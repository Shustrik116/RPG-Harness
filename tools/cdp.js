// Мини-драйвер headless-браузера через Chrome DevTools Protocol.
// Замена agent-browser: запускает Edge/Chrome, открывает страницу, выполняет
// переданный page-side JS и снимает скриншот. Без внешних зависимостей
// (использует встроенные в Node 22 fetch и WebSocket).
//
// Примеры:
//   node tools/cdp.js --url http://127.0.0.1:5204/chat/demo1 --wait 6000 \
//        --js .harness/steps/open-battle.js --shot .harness/shots/battle.png
//   node tools/cdp.js --url http://127.0.0.1:5204/ --js check.js
//
// Файл из --js выполняется в странице как async-IIFE. Что он вернёт —
// печатается в stdout как JSON (удобно читать агенту и парсить скриптам).
//
// Опции:
//   --url <url>        адрес страницы (обязательно)
//   --js <file>        page-side JS; выражение вычисляется как (async()=>{...})()
//   --shot <file>      путь для PNG-скриншота после выполнения --js
//   --clip <x,y,w,h>   снять не весь экран, а прямоугольник (крупный план деталей)
//   --clip-scale <n>   увеличение вырезанной области (по умолчанию 2)
//   --wait <ms>        пауза после навигации до первого шага (по умолчанию 5000)
//   --browser <path>   путь к msedge.exe / chrome.exe
//   --width/--height   размер окна (по умолчанию 1500x950)
//   --timeout <ms>     таймаут на один шаг (по умолчанию 60000)

const { spawn, spawnSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ===== аргументы =====

function parseArgs(argv) {
  const a = {};
  for (let i = 2; i < argv.length; i += 2) {
    const key = String(argv[i]).replace(/^--/, '');
    a[key] = argv[i + 1];
  }
  return a;
}

const args = parseArgs(process.argv);
const URL_ = args.url;
if (!URL_) {
  console.error('нужен --url <url>');
  process.exit(2);
}

const BROWSER = args.browser || process.env.PROBE_EDGE || findBrowser();
const WAIT = Number(args.wait || 5000);
const WIDTH = Number(args.width || 1500);
const HEIGHT = Number(args.height || 950);
const STEP_TIMEOUT = Number(args.timeout || 60000);
const CLIP = (args.clip || '').split(',').map(Number);
const CLIP_OK = CLIP.length === 4 && CLIP.every((n) => Number.isFinite(n));
const CLIP_SCALE = Number(args['clip-scale'] || 2);

function findBrowser() {
  const candidates = [
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
  ];
  for (const c of candidates) if (fs.existsSync(c)) return c;
  throw new Error('не найден Edge/Chrome, укажи --browser <path>');
}

// ===== CDP =====

async function launch() {
  const port = 9700 + Math.floor(Math.random() * 200);
  // В штатном запуске профиль живёт вне репозитория. Если системный temp недоступен
  // из-за ACL/песочницы, остаётся резервная папка, которая закрыта .gitignore.
  let userDir;
  try {
    userDir = fs.mkdtempSync(path.join(os.tmpdir(), 'rpg-harness-cdp-'));
  } catch (_) {
    userDir = path.join(__dirname, '.harness', 'profiles', 'cdp-' + Date.now() + '-' + Math.floor(Math.random() * 1e6));
    fs.mkdirSync(userDir, { recursive: true });
  }
  const proc = spawn(
    BROWSER,
    [
      '--headless=new',
      '--no-sandbox',
      '--disable-crash-reporter',
      '--disable-gpu',
      '--no-first-run',
      '--no-default-browser-check',
      '--window-size=' + WIDTH + ',' + HEIGHT,
      '--remote-debugging-port=' + port,
      '--user-data-dir=' + userDir,
      'about:blank',
    ],
    { stdio: 'ignore' }
  );
  // Браузер не должен держать event loop Node: иначе процесс не завершится сам
  // после того, как результат напечатан и профиль убран.
  proc.unref();

  let targets = null;
  for (let i = 0; i < 80; i++) {
    await sleep(500);
    try {
      const r = await fetch('http://127.0.0.1:' + port + '/json');
      targets = await r.json();
      if (targets && targets.length) break;
    } catch (_) {}
  }
  if (!targets) throw new Error('DevTools endpoint недоступен на порту ' + port);
  const page = targets.find((t) => t.type === 'page') || targets[0];

  const ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((res, rej) => {
    ws.onopen = res;
    ws.onerror = rej;
  });

  let id = 0;
  const pending = new Map();
  ws.onmessage = (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.id && pending.has(msg.id)) {
      pending.get(msg.id)(msg);
      pending.delete(msg.id);
    }
  };

  const send = (method, params) =>
    new Promise((resolve, reject) => {
      const myId = ++id;
      const timer = setTimeout(() => {
        pending.delete(myId);
        reject(new Error('таймаут CDP: ' + method));
      }, STEP_TIMEOUT);
      pending.set(myId, (m) => {
        clearTimeout(timer);
        m.error ? reject(new Error(method + ': ' + JSON.stringify(m.error))) : resolve(m.result);
      });
      ws.send(JSON.stringify({ id: myId, method, params }));
    });

  const evaluate = async (expr) => {
    const r = await send('Runtime.evaluate', {
      expression: expr,
      returnByValue: true,
      awaitPromise: true,
    });
    if (r.exceptionDetails) {
      const d = r.exceptionDetails.exception && r.exceptionDetails.exception.description;
      throw new Error(r.exceptionDetails.text + ' ' + (d || ''));
    }
    return r.result.value;
  };

  const shot = async (file) => {
    const params = { format: 'png' };
    // Крупный план: Page.captureScreenshot умеет вырезать прямоугольник и увеличивать его.
    if (CLIP_OK) {
      params.clip = { x: CLIP[0], y: CLIP[1], width: CLIP[2], height: CLIP[3], scale: CLIP_SCALE };
      params.captureBeyondViewport = true;
    }
    const r = await send('Page.captureScreenshot', params);
    fs.mkdirSync(path.dirname(path.resolve(file)), { recursive: true });
    fs.writeFileSync(file, Buffer.from(r.data, 'base64'));
  };

  return {
    send,
    evaluate,
    shot,
    close: async () => {
      // Просим Chromium завершиться штатно: так он закрывает дочерние процессы и
      // файловые базы профиля. Ограниченный таймаут не даёт зависнуть на сломанном CDP.
      try {
        await Promise.race([send('Browser.close', {}), sleep(2000)]);
      } catch (_) {}
      try {
        ws.close();
      } catch (_) {}
      // Edge/Chrome плодят дочерние процессы, которые держат файлы профиля:
      // proc.kill() убивает только родителя, поэтому валим всё дерево.
      // timeout обязателен: без него taskkill может не вернуться.
      try {
        if (process.platform === 'win32') {
          // На загруженном Edge завершение дерева иногда занимает больше пяти секунд:
          // прежний таймаут обрывал taskkill и оставлял профиль заблокированным.
          const killed = spawnSync('taskkill', ['/PID', String(proc.pid), '/T', '/F'], {
            stdio: 'ignore', timeout: 15000, windowsHide: true,
          });
          if (killed.error) proc.kill('SIGKILL');
        } else {
          proc.kill('SIGKILL');
        }
      } catch (_) {}
      // Профиль убираем ОТСОЕДИНЁННЫМ процессом, а не текущим процессом Node.
      // Удаление профиля внутри процесса недопустимо: на Windows каталог ещё
      // держат живые дочерние процессы браузера, и попытка удаления (и sync, и
      // async) зависает, удерживая handle libuv — процесс не завершается никогда,
      // а прогон молча теряет вывод. Отдельный Node-процесс ждёт освобождения
      // файлов и удаляет только переданный абсолютный каталог без участия shell.
      try {
        const cleanup = [
          "const fs=require('fs');",
          'const target=process.argv[1];',
          'let attempts=0;',
          'const remove=()=>{',
          '  try { fs.rmSync(target,{recursive:true,force:true,maxRetries:4,retryDelay:100}); } catch (_) {}',
          '  if (!fs.existsSync(target) || ++attempts>=120) process.exit(0);',
          '  setTimeout(remove,500);',
          '};',
          'setTimeout(remove,1000);',
        ].join('');
        spawn(process.execPath, ['-e', cleanup, userDir], {
          detached: true, stdio: 'ignore', windowsHide: true,
        }).unref();
      } catch (_) {}
    },
  };
}

// ===== основной сценарий =====

(async () => {
  const out = { url: URL_ };
  let cdp = null;
  try {
    cdp = await launch();
    await cdp.send('Page.enable');
    await cdp.send('Page.navigate', { url: URL_ });
    await sleep(WAIT);

    if (args.js) {
      const src = fs.readFileSync(args.js, 'utf8');
      // Обёртка: файл — это тело async-функции, выполняется в странице.
      out.result = await cdp.evaluate('(async () => {\n' + src + '\n})()');
    }

    if (args.shot) {
      await cdp.shot(args.shot);
      out.shot = path.resolve(args.shot);
    }
  } catch (e) {
    out.error = String((e && e.message) || e);
    process.exitCode = 1;
  } finally {
    // Результат печатаем ДО уборки и дожидаемся сброса буфера вывода, затем
    // завершаемся явно: браузер и профиль не должны держать процесс.
    await new Promise((res) => process.stdout.write(JSON.stringify(out, null, 2) + '\n', res));
    if (cdp) await cdp.close();
    process.exit(process.exitCode || 0);
  }
})();
