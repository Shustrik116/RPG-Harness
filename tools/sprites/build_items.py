"""
Сборка атласа предметов для инвентаря-тетриса.

Источник тайлов: Dungeon Crawl Stone Soup rltiles (CC0 / public domain),
https://github.com/crawl/crawl/tree/master/crawl-ref/source/rltiles/item
Иконки из первой версии атласа (legacy-items.png) переносятся как есть — сохранённые кампании не ломаются.

Результат:
  wwwroot/sprites/rpg-items.png  — атлас 32x32-тайлов, COLS колонок
  wwwroot/sprites/items.json     — каталог: id, название, категория, размер в клетках, слот
  wwwroot/css/pixel-items.css    — классы .pix-<id> (позиции в процентах: иконку можно масштабировать)

Запуск: python tools/sprites/build_items.py  (нужен Pillow; тайлы кэшируются в tools/sprites/.cache)
"""
import json
import os
import urllib.request

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CACHE = os.path.join(HERE, ".cache")
LEGACY = os.path.join(HERE, "legacy-items.png")
RAW_ROOT = "https://raw.githubusercontent.com/crawl/crawl/master/crawl-ref/source/rltiles/"
RAW = RAW_ROOT + "item/"
TILE = 32
COLS = 16

# Позиции иконок в первой версии атласа (10 колонок).
LEGACY_POS = {
    "helmet": (0, 0), "body": (1, 0), "gloves": (2, 0), "boots": (3, 0), "amulet": (4, 0),
    "ring": (5, 0), "ring2": (6, 0), "belt": (7, 0), "shield": (8, 0), "sword": (9, 0),
    "axe": (0, 1), "dagger": (1, 1), "mace": (2, 1), "bow": (3, 1), "staff": (4, 1),
    "potion_red": (5, 1), "potion_amber": (6, 1), "potion_cyan": (7, 1), "potion_emerald": (8, 1), "potion_smoky": (9, 1),
    "scroll": (0, 2), "map": (1, 2), "book": (2, 2), "key": (3, 2), "coins": (4, 2),
    "gem": (5, 2), "orb": (6, 2), "bottle": (7, 2), "bread": (8, 2), "meat": (9, 2),
    "apple": (0, 3), "bone": (1, 3), "horn": (2, 3), "lamp": (3, 3), "lantern": (4, 3),
    "sack": (5, 3), "phial": (6, 3), "mirror": (7, 3), "stone": (8, 3), "cloak": (9, 3),
    "avatar": (0, 4),
}

# (id, источник, название, категория, w, h, слот, двуручное, диагональный спрайт, ключевые слова)
# источник: "legacy" — из старого атласа, иначе путь внутри rltiles/item.
# Размеры в клетках — в духе Diablo / Path of Exile: мелочь 1x1, кинжал 1x2, меч 1x3,
# двуручник/посох/древковое 2x4 или 1x4, доспех 2x3, шлем/перчатки/сапоги 2x2.
# diag=True — оружие нарисовано по диагонали; в сетке его разворачивают вертикально.
W, S, A = "weapon", "shield", "armor"
CATALOG = [
    # ---- Одноручное оружие ----
    ("dagger", "legacy", "Кинжал", W, 1, 2, "Hand1", False, True, "кинжал нож"),
    ("athame", "weapon/athame1.png", "Ритуальный кинжал", W, 1, 2, "Hand1", False, True, "атам ритуальн жертвен"),
    ("short_sword", "weapon/short_sword1.png", "Короткий меч", W, 1, 2, "Hand1", False, True, "короткий меч гладиус"),
    ("sword", "legacy", "Меч", W, 1, 3, "Hand1", False, True, "меч клинок"),
    ("long_sword", "weapon/long_sword1.png", "Длинный меч", W, 1, 3, "Hand1", False, True, "длинный меч полуторн бастард"),
    ("rapier", "weapon/rapier1.png", "Рапира", W, 1, 3, "Hand1", False, True, "рапира шпага эсток"),
    ("scimitar", "weapon/scimitar1.png", "Скимитар", W, 1, 3, "Hand1", False, True, "скимитар сабл ятаган шамшир"),
    ("falchion", "weapon/falchion1.png", "Фальшион", W, 1, 3, "Hand1", False, True, "фальшион тесак мачете"),
    ("hand_axe", "weapon/hand_axe1.png", "Топорик", W, 1, 2, "Hand1", False, True, "топорик томагавк"),
    ("axe", "legacy", "Топор", W, 2, 3, "Hand1", False, True, "топор"),
    ("war_axe", "weapon/war_axe1.png", "Боевой топор", W, 2, 3, "Hand1", False, True, "боевой топор секир"),
    ("club", "weapon/club.png", "Дубина", W, 1, 3, "Hand1", False, True, "дубин палиц"),
    ("mace", "legacy", "Булава", W, 1, 3, "Hand1", False, True, "булава шестопёр пернач"),
    ("morningstar", "weapon/morningstar1.png", "Моргенштерн", W, 1, 3, "Hand1", False, True, "моргенштерн шипастая"),
    ("flail", "weapon/flail1.png", "Кистень", W, 2, 3, "Hand1", False, True, "кистень цеп"),
    ("hammer", "weapon/hammer1.png", "Боевой молот", W, 2, 3, "Hand1", False, True, "молот молоток клевец"),
    ("whip", "weapon/bullwhip.png", "Кнут", W, 2, 2, "Hand1", False, False, "кнут плеть хлыст"),
    ("demon_blade", "weapon/demon_blade.png", "Демонический клинок", W, 1, 3, "Hand1", False, True, "демонич проклят"),
    ("wand", "wand/gem_wood.png", "Волшебная палочка", W, 1, 2, "Hand1", False, True, "палочк жезл"),
    ("rod", "rod/rod03.png", "Скипетр", W, 1, 3, "Hand1", False, True, "скипетр"),
    # ---- Двуручное оружие ----
    ("greatsword", "weapon/greatsword1.png", "Двуручный меч", W, 2, 4, "Hand1", True, True, "двуручн меч эспадон клеймор"),
    ("battle_axe", "weapon/battle_axe1.png", "Секира", W, 2, 4, "Hand1", True, True, "двуручн топор секира"),
    ("executioner_axe", "weapon/executioner_axe1.png", "Топор палача", W, 2, 4, "Hand1", True, True, "палач"),
    ("great_mace", "weapon/mace_large1.png", "Тяжёлая булава", W, 2, 4, "Hand1", True, True, "тяжёл булава кувалд"),
    ("staff", "weapon/staff.png", "Посох", W, 1, 4, "Hand1", True, True, "посох"),
    ("quarterstaff", "weapon/quarterstaff.png", "Боевой шест", W, 1, 4, "Hand1", True, True, "шест боевой посох"),
    ("mage_staff", "staff/staff01.png", "Посох мага", W, 1, 4, "Hand1", True, True, "посох мага волшебн посох"),
    ("elder_staff", "staff/staff05.png", "Древний посох", W, 1, 4, "Hand1", True, True, "древн посох архимаг"),
    ("spear", "weapon/spear1.png", "Копьё", W, 1, 4, "Hand1", True, True, "копь пик рогатин"),
    ("trident", "weapon/trident1.png", "Трезубец", W, 2, 4, "Hand1", True, True, "трезуб острог"),
    ("halberd", "weapon/halberd1.png", "Алебарда", W, 2, 4, "Hand1", True, True, "алебард бердыш"),
    ("glaive", "weapon/glaive1.png", "Глефа", W, 2, 4, "Hand1", True, True, "глеф нагинат"),
    ("scythe", "weapon/scythe1.png", "Коса", W, 2, 4, "Hand1", True, True, "коса серп"),
    # ---- Стрелковое ----
    ("bow", "legacy", "Лук", W, 2, 3, "Hand1", True, True, "лук"),
    ("shortbow", "weapon/ranged/shortbow1.png", "Короткий лук", W, 2, 3, "Hand1", True, True, "короткий лук"),
    ("longbow", "weapon/ranged/longbow1.png", "Длинный лук", W, 2, 4, "Hand1", True, True, "длинный лук"),
    ("crossbow", "weapon/ranged/arbalest1.png", "Арбалет", W, 2, 3, "Hand1", True, False, "арбалет самострел"),
    ("sling", "weapon/ranged/sling1.png", "Праща", W, 1, 2, "Hand1", False, False, "праща"),
    ("hand_cannon", "weapon/ranged/hand_cannon.png", "Ручная пищаль", W, 2, 2, "Hand1", True, False, "пищаль мушкет пистол ружь аркебуз"),
    ("javelin", "weapon/ranged/javelin1.png", "Дротики", W, 1, 3, None, False, True, "дротик метательн копь сулиц"),
    ("arrows", "weapon/ranged/arrow2.png", "Стрелы", W, 1, 3, None, False, True, "стрел колчан"),
    ("bolts", "weapon/ranged/crossbow_bolt1.png", "Арбалетные болты", W, 1, 2, None, False, True, "болт"),
    ("darts", "weapon/ranged/dart1.png", "Метательные ножи", W, 1, 1, None, False, False, "метательн нож сюрикен"),
    ("boomerang", "weapon/ranged/boomerang1.png", "Бумеранг", W, 1, 2, None, False, False, "бумеранг"),
    ("net", "weapon/ranged/throwing_net.png", "Сеть", W, 2, 2, None, False, False, "сеть сетк"),
    # ---- Щиты и фокусы ----
    ("buckler", "armour/shields/buckler1.png", "Баклер", S, 2, 2, "Hand2", False, False, "баклер кулачн щит"),
    ("shield", "legacy", "Щит", S, 2, 2, "Hand2", False, False, "щит"),
    ("kite_shield", "armour/shields/kite_shield1.png", "Каплевидный щит", S, 2, 3, "Hand2", False, False, "каплевидн рыцарск щит"),
    ("tower_shield", "armour/shields/tower_shield1.png", "Ростовой щит", S, 2, 4, "Hand2", False, False, "ростов башенн павез щит"),
    ("focus_orb", "armour/shields/orb.png", "Сфера-фокус", S, 2, 2, "Hand2", False, False, "фокус сфера"),
    # ---- Доспехи ----
    ("body", "legacy", "Доспех", A, 2, 3, "Body", False, False, "доспех броня кирас"),
    ("robe", "armour/robe1.png", "Мантия", A, 2, 3, "Body", False, False, "мантия ряса роба балахон одеян"),
    ("mage_robe", "armour/robe_ego1.png", "Расшитая мантия", A, 2, 3, "Body", False, False, "расшит мантия чародей"),
    ("leather_armour", "armour/leather_armour1.png", "Кожаный доспех", A, 2, 3, "Body", False, False, "кожан доспех куртк стёган"),
    ("animal_skin", "armour/animal_skin1.png", "Звериная шкура", A, 2, 3, "Body", False, False, "шкур мех"),
    ("ring_mail", "armour/ring_mail1.png", "Кольчужная рубаха", A, 2, 3, "Body", False, False, "кольчужн рубах"),
    ("chain_mail", "armour/chain_mail1.png", "Кольчуга", A, 2, 3, "Body", False, False, "кольчуг хауберк"),
    ("scale_mail", "armour/scale_mail1.png", "Чешуйчатый доспех", A, 2, 3, "Body", False, False, "чешуйчат бригантин ламелляр"),
    ("plate", "armour/plate1.png", "Латы", A, 2, 3, "Body", False, False, "лат латн"),
    ("dragon_armour", "armour/fire_dragon_armour.png", "Драконья чешуя", A, 2, 3, "Body", False, False, "драконь"),
    ("cloak", "legacy", "Плащ", A, 2, 3, "Cloak", False, False, "плащ"),
    ("leather_cloak", "armour/cloak1_leather.png", "Кожаный плащ", A, 2, 3, "Cloak", False, False, "кожан плащ"),
    ("fine_cloak", "armour/cloak3.png", "Дорожный плащ", A, 2, 3, "Cloak", False, False, "дорожн накидк пелерин"),
    ("scarf", "armour/scarf1.png", "Шарф", A, 2, 2, "Cloak", False, False, "шарф палантин"),
    # ---- Голова / руки / ноги / пояс ----
    ("helmet", "legacy", "Шлем", A, 2, 2, "Helmet", False, False, "шлем"),
    ("leather_helm", "armour/headgear/elven_leather_helm.png", "Кожаный шлем", A, 2, 2, "Helmet", False, False, "кожан шлем капюшон"),
    ("great_helm", "armour/headgear/helmet3.png", "Топфхельм", A, 2, 2, "Helmet", False, False, "топфхельм закрыт шлем бацинет"),
    ("horned_helm", "armour/headgear/helmet2.png", "Рогатый шлем", A, 2, 2, "Helmet", False, False, "рогат шлем"),
    ("wizard_hat", "armour/headgear/wizard_hat.png", "Колпак мага", A, 2, 2, "Helmet", False, False, "колпак остроконечн шляп"),
    ("hat", "armour/headgear/hat1.png", "Шляпа", A, 2, 2, "Helmet", False, False, "шляп"),
    ("archer_hat", "armour/headgear/hat_archer.png", "Шляпа с пером", A, 2, 2, "Helmet", False, False, "перо берет"),
    ("gloves", "legacy", "Перчатки", A, 2, 2, "Gloves", False, False, "перчатк рукавиц"),
    ("gauntlets", "armour/glove4.png", "Латные рукавицы", A, 2, 2, "Gloves", False, False, "латн рукавиц наруч"),
    ("fine_gloves", "armour/glove2.png", "Тонкие перчатки", A, 2, 2, "Gloves", False, False, "тонк перчатк"),
    ("boots", "legacy", "Сапоги", A, 2, 2, "Boots", False, False, "сапог ботинк"),
    ("heavy_boots", "armour/boots2.png", "Тяжёлые сапоги", A, 2, 2, "Boots", False, False, "тяжёл сапог поножи"),
    ("fine_boots", "armour/boots_ego1.png", "Мягкие сапоги", A, 2, 2, "Boots", False, False, "мягк сапог туфл"),
    ("belt", "draw:belt", "Пояс", A, 2, 1, "Belt", False, False, "пояс ремень кушак"),
    # ---- Украшения ----
    ("amulet", "legacy", "Амулет", "jewelry", 1, 1, "Amulet", False, False, "амулет"),
    ("amulet_ruby", "amulet/ruby.png", "Рубиновый амулет", "jewelry", 1, 1, "Amulet", False, False, "рубинов амулет"),
    ("amulet_silver", "amulet/silver.png", "Серебряный медальон", "jewelry", 1, 1, "Amulet", False, False, "медальон серебрян"),
    ("amulet_bone", "amulet/bone.png", "Костяной оберег", "jewelry", 1, 1, "Amulet", False, False, "оберег костян"),
    ("amulet_skull", "amulet/randarts/skull.png", "Амулет-череп", "jewelry", 1, 1, "Amulet", False, False, "череп некро"),
    ("amulet_sun", "amulet/randarts/sun.png", "Солнечный амулет", "jewelry", 1, 1, "Amulet", False, False, "солнечн святой символ"),
    ("amulet_scarab", "amulet/randarts/scarab.png", "Скарабей", "jewelry", 1, 1, "Amulet", False, False, "скараб"),
    ("ring", "legacy", "Кольцо", "jewelry", 1, 1, "Ring1", False, False, "кольц перстень"),
    ("ring2", "legacy", "Перстень", "jewelry", 1, 1, "Ring1", False, False, "перстень"),
    ("ring_gold", "ring/gold.png", "Золотое кольцо", "jewelry", 1, 1, "Ring1", False, False, "золот кольц"),
    ("ring_silver", "ring/silver.png", "Серебряное кольцо", "jewelry", 1, 1, "Ring1", False, False, "серебрян кольц"),
    ("ring_iron", "ring/iron.png", "Железное кольцо", "jewelry", 1, 1, "Ring1", False, False, "железн кольц"),
    ("ring_ruby", "ring/ruby.png", "Рубиновый перстень", "jewelry", 1, 1, "Ring1", False, False, "рубин"),
    ("ring_emerald", "ring/emerald.png", "Изумрудный перстень", "jewelry", 1, 1, "Ring1", False, False, "изумруд"),
    ("ring_snake", "ring/randarts/snake.png", "Кольцо-змея", "jewelry", 1, 1, "Ring1", False, False, "змеин"),
    ("ring_eye", "ring/randarts/eye.png", "Кольцо с оком", "jewelry", 1, 1, "Ring1", False, False, "око глаз"),
    # ---- Зелья ----
    ("potion_red", "potion/ruby.png", "Красное зелье", "potion", 1, 1, None, False, False, "лечен исцелен красн зель"),
    ("potion_amber", "potion/orange.png", "Янтарное зелье", "potion", 1, 1, None, False, False, "янтарн"),
    ("potion_cyan", "legacy", "Голубое зелье", "potion", 1, 1, None, False, False, "ман голуб"),
    ("potion_emerald", "legacy", "Зелёное зелье", "potion", 1, 1, None, False, False, "зелён противояд"),
    ("potion_smoky", "potion/cloudy.png", "Дымчатое зелье", "potion", 1, 1, None, False, False, "дымчат"),
    ("potion_blue", "potion/brilliant_blue.png", "Синее зелье", "potion", 1, 1, None, False, False, "син зель"),
    ("potion_golden", "potion/golden.png", "Золотое зелье", "potion", 1, 1, None, False, False, "золот зель эликсир"),
    ("potion_pink", "potion/pink.png", "Розовое зелье", "potion", 1, 1, None, False, False, "розов любовн"),
    ("potion_black", "potion/black.png", "Чёрное зелье", "potion", 1, 1, None, False, False, "чёрн яд"),
    ("potion_white", "potion/white.png", "Белое зелье", "potion", 1, 1, None, False, False, "бел молок"),
    ("potion_bubbly", "potion/bubbly.png", "Шипучее зелье", "potion", 1, 1, None, False, False, "шипуч пузыр"),
    ("potion_murky", "potion/murky.png", "Мутное зелье", "potion", 1, 1, None, False, False, "мутн отвар"),
    ("potion_magenta", "potion/magenta.png", "Пурпурное зелье", "potion", 1, 1, None, False, False, "пурпурн фиолет"),
    ("phial", "legacy", "Флакон", "potion", 1, 1, None, False, False, "флакон склянк"),
    ("bottle", "legacy", "Бутыль", "potion", 1, 2, None, False, False, "бутыл вин эль"),
    # ---- Свитки и книги ----
    ("scroll", "scroll/scroll.png", "Свиток", "scroll", 1, 1, None, False, False, "свиток"),
    ("scroll_red", "scroll/scroll-red.png", "Свиток с красной печатью", "scroll", 1, 1, None, False, False, "печат указ"),
    ("scroll_blue", "scroll/scroll-blue.png", "Синий свиток", "scroll", 1, 1, None, False, False, "син свиток"),
    ("scroll_green", "scroll/scroll-green.png", "Зелёный свиток", "scroll", 1, 1, None, False, False, "зелён свиток"),
    ("parchment", "parchment/base_parchment_low_level.png", "Пергамент", "scroll", 1, 1, None, False, False, "пергамент письм записк"),
    ("map", "parchment/base_parchment_high_level.png", "Карта", "scroll", 2, 2, None, False, False, "карт"),
    ("book", "legacy", "Книга", "book", 2, 2, None, False, False, "книг том"),
    ("book_red", "book/red.png", "Красный фолиант", "book", 2, 2, None, False, False, "фолиант"),
    ("book_green", "book/dark_green.png", "Зелёный гримуар", "book", 2, 2, None, False, False, "гримуар"),
    ("book_blue", "book/metal_blue.png", "Окованная книга", "book", 2, 2, None, False, False, "окован"),
    ("book_gold", "book/gold.png", "Золочёная книга", "book", 2, 2, None, False, False, "золочён священн писан молитвенн"),
    ("book_dead", "book/book_of_the_dead.png", "Книга мёртвых", "book", 2, 2, None, False, False, "мёртв некроном"),
    ("manual", "book/manual1.png", "Учебник", "book", 2, 2, None, False, False, "учебник руководств дневник журнал"),
    # ---- Еда ----
    ("bread", "legacy", "Хлеб", "food", 2, 1, None, False, False, "хлеб лепёшк"),
    ("meat", "legacy", "Мясо", "food", 1, 1, None, False, False, "мяс жарк окорок"),
    ("jerky", "food/beef_jerky.png", "Вяленое мясо", "food", 1, 1, None, False, False, "вялен солонин сухпа паёк"),
    ("sausage", "food/sausage.png", "Колбаса", "food", 1, 1, None, False, False, "колбас"),
    ("cheese", "food/cheese.png", "Сыр", "food", 1, 1, None, False, False, "сыр"),
    ("apple", "legacy", "Яблоко", "food", 1, 1, None, False, False, "яблок"),
    ("pear", "food/pear.png", "Груша", "food", 1, 1, None, False, False, "груш"),
    ("orange", "food/orange.png", "Апельсин", "food", 1, 1, None, False, False, "апельсин"),
    ("grapes", "food/grape.png", "Виноград", "food", 1, 1, None, False, False, "виноград ягод"),
    ("strawberry", "food/strawberry.png", "Земляника", "food", 1, 1, None, False, False, "земляник клубник"),
    ("honeycomb", "food/honeycomb.png", "Соты", "food", 1, 1, None, False, False, "сот мёд"),
    # ---- Ценности ----
    ("coins", "gold/05.png", "Монеты", "valuable", 1, 1, None, False, False, "монет золот серебр медяк"),
    ("gold_pile", "gold/10.png", "Горсть золота", "valuable", 1, 1, None, False, False, "горст куча золот"),
    ("gem", "gem/dungeon_found_whole.png", "Самоцвет", "valuable", 1, 1, None, False, False, "самоцвет драгоцен камен"),
    ("gem_red", "gem/tomb_found_whole.png", "Красный самоцвет", "valuable", 1, 1, None, False, False, "рубин красн камен"),
    ("gem_green", "gem/snake_found_whole.png", "Зелёный самоцвет", "valuable", 1, 1, None, False, False, "изумруд зелён камен"),
    ("gem_blue", "gem/shoals_found_whole.png", "Синий самоцвет", "valuable", 1, 1, None, False, False, "сапфир син камен"),
    ("gem_violet", "gem/depths_found_whole.png", "Фиолетовый самоцвет", "valuable", 1, 1, None, False, False, "аметист фиолет"),
    ("gem_yellow", "gem/orc_found_whole.png", "Жёлтый самоцвет", "valuable", 1, 1, None, False, False, "топаз цитрин жёлт камен"),
    ("rune", "misc/runes/generic.png", "Руна", "valuable", 1, 1, None, False, False, "рун"),
    ("talisman_dragon", "talisman/dragon.png", "Драконий талисман", "valuable", 1, 1, None, False, False, "талисман драко"),
    ("talisman_spider", "talisman/spider.png", "Паучий талисман", "valuable", 1, 1, None, False, False, "паук"),
    ("idol", "talisman/statue.png", "Статуэтка", "valuable", 1, 2, None, False, False, "статуэтк идол фигурк"),
    # ---- Инструменты и снаряжение ----
    ("key", "draw:key", "Ключ", "tool", 1, 1, None, False, False, "ключ"),
    ("lamp", "legacy", "Масляная лампа", "tool", 1, 1, None, False, False, "ламп"),
    ("lantern", "misc/misc_lamp.png", "Фонарь", "tool", 1, 2, None, False, False, "фонар факел"),
    ("sack", "legacy", "Мешок", "tool", 2, 2, None, False, False, "мешок сумк котомк рюкзак"),
    ("horn", "legacy", "Рог", "tool", 1, 2, None, False, False, "рог горн"),
    ("tambourine", "misc/misc_tambourine.png", "Бубен", "tool", 2, 2, None, False, False, "бубен барабан музыкальн лютн"),
    ("fan", "misc/misc_fan.png", "Веер", "tool", 1, 2, None, False, False, "веер"),
    ("mirror", "legacy", "Зеркало", "tool", 1, 2, None, False, False, "зеркал"),
    ("box", "misc/misc_box_of_beasts.png", "Шкатулка", "tool", 2, 2, None, False, False, "шкатулк ларец сундучок короб"),
    ("lightning_rod", "misc/misc_lightning_rod.png", "Громовой жезл", "tool", 1, 3, None, False, True, "громов молни"),
    ("vane", "misc/misc_vane.png", "Флюгер", "tool", 1, 2, None, False, False, "флюгер компас"),
    ("gizmo", "gizmo/gizmo3.png", "Механизм", "tool", 1, 1, None, False, False, "механизм шестерн устройств прибор"),
    ("torch", "@player/hand2/misc/torch.png", "Факел", "tool", 1, 2, None, False, False, "факел"),
    ("pickaxe", "@player/hand1/pick_axe.png", "Кирка", "tool", 1, 2, None, False, False, "кирк кайл лопат"),
    ("lens", "legacy:scroll", "Лупа", "tool", 1, 1, None, False, False, "лупа линз увеличит"),
    ("compass", "legacy:map", "Компас", "tool", 1, 1, None, False, False, "компас"),
    ("skull_lantern", "legacy:lantern", "Череп-светильник", "tool", 1, 2, None, False, False, "череп светильн"),
    ("drum", "misc/misc_tremorstones.png", "Барабан", "tool", 1, 1, None, False, False, "барабан бочонок"),
    ("voucher", "misc/misc_voucher.png", "Билет", "tool", 1, 1, None, False, False, "билет пропуск грамот векс"),
    # ---- Прочее ----
    ("glass_orb", "legacy:gem", "Хрустальный шар", "misc", 1, 1, None, False, False, "стеклянн шар хрустальн кристалл"),
    ("orb", "legacy", "Сфера", "misc", 1, 1, None, False, False, "сфера орб шар"),
    ("stone", "legacy", "Камень", "misc", 1, 1, None, False, False, "камень булыжник"),
    ("bone", "food/bone1.png", "Кость", "misc", 1, 1, None, False, False, "кост"),
    ("skull", "food/bone_humanoid1.png", "Череп", "misc", 1, 1, None, False, False, "череп"),
    ("avatar", "legacy", "Силуэт героя", "misc", 1, 1, None, False, False, ""),
]

# ===== Расширение каталога (дописывать только в конец: позиции в атласе = порядок) =====
# Источник "painted:N" — клетка N листа combat-icons-loot.png, уменьшенная до 32x32 (материалы и трофеи).
# Предметы с art=True — именные артефакты: в обычных лавках не продаются, генератор добычи берёт их облик
# для эпических и легендарных вещей.
M = "material"
EXTRA = [
    # ---- Материалы и трофеи (с тварей и из природы) ----
    ("fang", "painted:45", "Клык", M, 1, 1, None, False, False, "клык коготь зуб бивень"),
    ("horned_skull", "painted:46", "Рогатый череп", M, 1, 1, None, False, False, "рогат череп рога трофей"),
    ("scales", "painted:47", "Чешуя", M, 1, 1, None, False, False, "чешу панцир"),
    ("herbs", "painted:48", "Лечебные травы", M, 1, 1, None, False, False, "трав корен цвет лист зверобой шалфей"),
    ("mushroom", "painted:49", "Гриб", M, 1, 1, None, False, False, "гриб спор мухомор трюфел"),
    ("ingot", "painted:51", "Слиток", M, 1, 1, None, False, False, "слиток металл руда желез сталь серебр мифрил"),
    ("fur", "painted:52", "Мех", M, 1, 1, None, False, False, "мех шерст перья перо пух"),
    ("leather", "painted:53", "Выделанная кожа", M, 2, 1, None, False, False, "кож шкур выделан"),
    ("rope", "painted:54", "Верёвка", "tool", 1, 1, None, False, False, "верёв верев канат аркан"),
    ("crystals", "painted:55", "Кристаллы", M, 1, 1, None, False, False, "кристал осколк друз эссенц"),
    # ---- Именные артефакты: оружие ----
    ("sword_singing", "weapon/artefact/spwpn_singing_sword.png", "Поющий меч", W, 1, 3, "Hand1", False, True, "поющ меч золот клинок", True),
    ("sword_power", "weapon/artefact/spwpn_sword_of_power.png", "Меч могущества", W, 1, 3, "Hand1", False, True, "могуществ меч красн клинок", True),
    ("katana", "weapon/artefact/urand_katana.png", "Катана", W, 1, 3, "Hand1", False, True, "катан тати вакидзаси", True),
    ("sword_blood", "weapon/artefact/urand_bloodbane.png", "Кровавый клинок", W, 1, 3, "Hand1", False, True, "кровав клинок", True),
    ("sword_flame", "weapon/artefact/urand_flaming_death.png", "Пылающий клинок", W, 1, 3, "Hand1", False, True, "пылающ огнен меч пламен клинок", True),
    ("axe_frost", "weapon/artefact/urand_frostbite.png", "Ледяная секира", W, 2, 3, "Hand1", False, True, "ледян топор ледян секир морозн", True),
    ("mace_holy", "weapon/artefact/urand_undeadhunter.png", "Булава охотника на нежить", W, 1, 3, "Hand1", False, True, "свят булав серебрян булав охотник на нежить", True),
    ("lance_wyrm", "weapon/artefact/urand_wyrmbane.png", "Драконоборец", W, 1, 4, "Hand1", True, True, "драконобор копьё драконоборц", True),
    ("bow_storm", "weapon/artefact/urand_storm_bow.png", "Штормовой лук", W, 2, 4, "Hand1", True, True, "штормов лук грозов лук", True),
    ("maul_skull", "weapon/artefact/urand_skullcrusher.png", "Черепокол", W, 2, 4, "Hand1", True, True, "черепокол череполом", True),
    ("dagger_vampire", "weapon/artefact/spwpn_vampires_tooth.png", "Клык вампира", W, 1, 2, "Hand1", False, True, "вампир кинжал клык вампира", True),
    ("spear_crystal", "weapon/artefact/urand_crystal_spear.png", "Хрустальное копьё", W, 1, 4, "Hand1", True, True, "хрустал копь", True),
    ("sword_arc", "weapon/artefact/urand_arc_blade.png", "Грозовой клинок", W, 1, 3, "Hand1", False, True, "грозов клинок молни меч", True),
    ("axe_holy", "weapon/artefact/spwpn_holy_axe.png", "Святая секира", W, 2, 4, "Hand1", True, True, "свят секир золот секир", True),
    ("axe_demon", "weapon/artefact/spwpn_demon_axe.png", "Демоническая секира", W, 2, 4, "Hand1", True, True, "демонич секир", True),
    ("staff_venom", "weapon/artefact/spwpn_staff_of_olgreb.png", "Посох яда", W, 1, 4, "Hand1", True, True, "посох яда ядовит посох", True),
    ("maul_dark", "weapon/artefact/urand_dark_maul.png", "Тёмная кувалда", W, 2, 4, "Hand1", True, True, "тёмн кувалд тёмн молот", True),
    ("dagger_precise", "weapon/artefact/urand_knife_of_accuracy.png", "Нож меткости", W, 1, 2, "Hand1", False, True, "нож меткост", True),
    ("crossbow_sniper", "weapon/artefact/urand_sniper.png", "Снайперский арбалет", W, 2, 3, "Hand1", True, False, "снайпер арбалет", True),
    ("staff_elements", "weapon/artefact/urand_elemental.png", "Посох стихий", W, 1, 4, "Hand1", True, True, "посох стихий стихийн посох", True),
    ("staff_fire", "weapon/artefact/urand_firestarter.png", "Посох пламени", W, 1, 4, "Hand1", True, True, "посох пламен огнен посох", True),
    ("wand_winter", "weapon/artefact/urand_fimbulwinter.png", "Жезл вечной зимы", W, 1, 2, "Hand1", False, True, "жезл зимы ледян жезл", True),
    ("staff_battle", "weapon/artefact/urand_staff_of_battle.png", "Боевой посох чародея", W, 1, 4, "Hand1", True, True, "боев посох чародея", True),
    ("whip_spell", "weapon/artefact/urand_spellbinder.png", "Плеть-заклинательница", W, 2, 2, "Hand1", False, False, "заклинательн плеть", True),
    ("cutlass", "weapon/artefact/urand_cutlass.png", "Абордажная сабля", W, 1, 3, "Hand1", False, True, "абордаж сабл кортик", True),
    ("scimitar_bloom", "weapon/artefact/urand_hana_scimitar.png", "Цветущий ятаган", W, 1, 3, "Hand1", False, True, "цветущ ятаган", True),
    ("sword_leech", "weapon/artefact/urand_leech.png", "Клинок-пиявка", W, 1, 3, "Hand1", False, True, "пиявк клинок волнист", True),
    ("sabre_crimson", "weapon/artefact/urand_morg.png", "Багровая сабля", W, 1, 3, "Hand1", False, True, "багров сабл", True),
    ("scythe_curse", "weapon/artefact/spwpn_scythe_of_curses.png", "Проклятая коса", W, 2, 4, "Hand1", True, True, "проклят кос", True),
    ("sceptre_torment", "weapon/artefact/spwpn_sceptre_of_torment.png", "Скипетр мучений", W, 1, 3, "Hand1", False, True, "скипетр мучен", True),
    ("lance_order", "weapon/artefact/urand_order.png", "Копьё порядка", W, 1, 4, "Hand1", True, True, "копьё порядк рыцарск копь", True),
    ("lance_force", "weapon/artefact/urand_force_lance.png", "Силовое копьё", W, 1, 4, "Hand1", True, True, "силов копь", True),
    # ---- Именные артефакты: доспехи и облачение ----
    ("helm_dragon", "armour/artefact/urand_dragonmask.png", "Драконья маска", A, 2, 2, "Helmet", False, False, "драконь маск", True),
    ("crown", "armour/artefact/urand_crown_of_vainglory.png", "Корона", A, 2, 2, "Helmet", False, False, "корон диадем венец", True),
    ("gloves_power", "armour/artefact/urand_power_gloves.png", "Перчатки мощи", A, 2, 2, "Gloves", False, False, "перчатки мощи", True),
    ("gauntlets_war", "armour/artefact/urand_war.png", "Рукавицы войны", A, 2, 2, "Gloves", False, False, "рукавицы войны", True),
    ("boots_seven", "armour/artefact/urand_seven_league_boots.png", "Семимильные сапоги", A, 2, 2, "Boots", False, False, "семимильн сапог скороход", True),
    ("cloak_night", "armour/artefact/urand_night.png", "Плащ ночи", A, 2, 3, "Cloak", False, False, "плащ ночи ночн плащ", True),
    ("cloak_starlight", "armour/artefact/urand_starlight.png", "Звёздный плащ", A, 2, 3, "Cloak", False, False, "звёздн плащ звездн плащ", True),
    ("hat_bear", "armour/artefact/urand_bear.png", "Медвежья шапка", A, 2, 2, "Helmet", False, False, "медвеж шапк", True),
    ("cloak_rat", "armour/artefact/urand_ratskin_cloak.png", "Плащ из крысиных шкур", A, 2, 3, "Cloak", False, False, "крысин плащ", True),
    ("mail_salamander", "armour/artefact/urand_salamander.png", "Саламандровая кольчуга", A, 2, 3, "Body", False, False, "саламандр кольчуг", True),
    ("plate_orange", "armour/artefact/urand_orange_crystal.png", "Янтарные латы", A, 2, 3, "Body", False, False, "янтарн лат", True),
    ("scale_dragonking", "armour/artefact/urand_dragon_king.png", "Чешуя драконьего короля", A, 2, 3, "Body", False, False, "драконьего короля чешуя", True),
    ("cloak_thief", "armour/artefact/urand_thief.png", "Плащ вора", A, 2, 3, "Cloak", False, False, "плащ вора воровск плащ", True),
    ("hat_council", "armour/artefact/urand_high_council.png", "Колпак верховного совета", A, 2, 2, "Helmet", False, False, "колпак совета зелён колпак", True),
    ("armor_justicar", "armour/artefact/urand_justicars_regalia.png", "Регалии юстициара", A, 2, 3, "Body", False, False, "юстициар регали паладинск лат", True),
    ("robe_vines", "armour/artefact/urand_vines.png", "Одеяние лоз", A, 2, 3, "Body", False, False, "лоз одеяни друидск мантия", True),
    ("boots_spider", "armour/artefact/urand_spider.png", "Паучьи сапоги", A, 2, 2, "Boots", False, False, "паучь сапог", True),
    ("skull_cursed", "armour/artefact/urand_skull_of_zonguldrok.png", "Проклятый череп", "valuable", 1, 1, None, False, False, "проклят череп", True),
    ("shield_storm", "armour/artefact/urand_storm_queen.png", "Щит королевы бурь", S, 2, 3, "Hand2", False, False, "щит бурь грозов щит", True),
    ("shield_gong", "armour/artefact/urand_gong.png", "Щит-гонг", S, 2, 2, "Hand2", False, False, "гонг бронзов щит", True),
    ("boots_mountain", "armour/artefact/urand_mountain.png", "Горные сапоги", A, 2, 2, "Boots", False, False, "горн сапог", True),
    ("armor_bone", "armour/artefact/urand_bone_scales.png", "Костяная броня", A, 2, 3, "Body", False, False, "костян брон костян доспех", True),
    ("slippers", "armour/artefact/urand_slippers.png", "Туфли танцора", A, 2, 2, "Boots", False, False, "туфл танцор башмак", True),
    # ---- Доспехи из шкур чудовищ ----
    ("plate_crystal", "armour/crystal_plate.png", "Кристальные латы", A, 2, 3, "Body", False, False, "кристал лат", True),
    ("dragon_storm_armour", "armour/storm_dragon_armour.png", "Доспех грозового дракона", A, 2, 3, "Body", False, False, "грозов дракон доспех син дракон"),
    ("dragon_ice_armour", "armour/ice_dragon_armour.png", "Доспех ледяного дракона", A, 2, 3, "Body", False, False, "ледян дракон бел дракон"),
    ("dragon_shadow_armour", "armour/shadow_dragon_armour.png", "Доспех теневого дракона", A, 2, 3, "Body", False, False, "тенев дракон чёрн дракон"),
    ("dragon_gold_armour", "armour/golden_dragon_armour.png", "Доспех золотого дракона", A, 2, 3, "Body", False, False, "золот дракон"),
    ("dragon_acid_armour", "armour/acid_dragon_armour.png", "Доспех кислотного дракона", A, 2, 3, "Body", False, False, "кислот дракон жёлт дракон"),
    ("dragon_swamp_armour", "armour/swamp_dragon_armour.png", "Доспех болотного дракона", A, 2, 3, "Body", False, False, "болотн дракон зелён дракон"),
    ("dragon_pearl_armour", "armour/pearl_dragon_armour.png", "Доспех жемчужного дракона", A, 2, 3, "Body", False, False, "жемчужн дракон"),
    ("troll_hide", "armour/troll_leather_armour.png", "Доспех из шкуры тролля", A, 2, 3, "Body", False, False, "тролл шкур доспех из шкуры"),
    ("plate_dark", "armour/plate2.png", "Воронёные латы", A, 2, 3, "Body", False, False, "воронён лат чёрн лат"),
    ("brigandine", "armour/chain_mail2.png", "Бригантина", A, 2, 3, "Body", False, False, "бригантин куяк"),
    ("robe_teal", "armour/robe2.png", "Бирюзовая мантия", A, 2, 3, "Body", False, False, "бирюзов мантия"),
    ("robe_ornate", "armour/robe_art1.png", "Расшитое одеяние", A, 2, 3, "Body", False, False, "расшит одеяни парадн мантия"),
    ("cloak_grey", "armour/cloak2.png", "Серый плащ", A, 2, 3, "Cloak", False, False, "сер плащ"),
    ("cloak_royal", "armour/cloak4.png", "Пурпурный плащ", A, 2, 3, "Cloak", False, False, "пурпурн плащ королевск мантия"),
    ("gauntlets_ornate", "armour/glove5.png", "Узорные рукавицы", A, 2, 2, "Gloves", False, False, "узорн рукавиц"),
    ("boots_elven", "armour/boots_art1.png", "Эльфийские сапоги", A, 2, 2, "Boots", False, False, "эльфийск сапог"),
    ("helm_ornate", "armour/headgear/helmet_art1.png", "Узорный шлем", A, 2, 2, "Helmet", False, False, "узорн шлем"),
    ("helm_plumed", "armour/headgear/helmet_ego2.png", "Шлем с плюмажем", A, 2, 2, "Helmet", False, False, "плюмаж гребен шлем"),
    ("hat_explorer", "armour/headgear/hat_explorer.png", "Шляпа путешественника", A, 2, 2, "Helmet", False, False, "шляп путешественник широкопол"),
    ("cap_jester", "armour/headgear/cap_jester.png", "Колпак шута", A, 2, 2, "Helmet", False, False, "шут колпак"),
    ("helm_barbute", "armour/headgear/helmet5.png", "Барбют", A, 2, 2, "Helmet", False, False, "барбют стальн шлем"),
    ("shield_heraldic", "armour/shields/kite_shield2.png", "Геральдический щит", S, 2, 3, "Hand2", False, False, "геральд герб щит"),
    ("tower_shield_dark", "armour/shields/tower_shield2.png", "Тёмный ростовой щит", S, 2, 4, "Hand2", False, False, "тёмн ростов щит"),
    ("buckler_bronze", "armour/shields/buckler2.png", "Бронзовый баклер", S, 2, 2, "Hand2", False, False, "бронзов баклер кругл щит"),
    ("orb_chaos", "armour/shields/orb_randart1.png", "Сфера хаоса", S, 2, 2, "Hand2", False, False, "сфера хаоса", True),
    # ---- Оружие ----
    ("bardiche", "weapon/bardiche1.png", "Бердыш", W, 2, 4, "Hand1", True, True, "бердыш"),
    ("broad_axe", "weapon/broad_axe1.png", "Широкий топор", W, 2, 3, "Hand1", False, True, "широк топор"),
    ("dire_flail", "weapon/dire_flail1.png", "Тяжёлый цеп", W, 2, 4, "Hand1", True, True, "тяжёл цеп молотил"),
    ("double_sword", "weapon/double_sword.png", "Парный клинок", W, 1, 3, "Hand1", False, True, "парн клинок двойн меч"),
    ("eveningstar", "weapon/eveningstar1.png", "Шипастая звезда", W, 1, 3, "Hand1", False, True, "шипаст звезд"),
    ("giant_club", "weapon/giant_club.png", "Огромная дубина", W, 2, 4, "Hand1", True, True, "огромн дубин дубина великана"),
    ("spiked_club", "weapon/giant_spiked_club.png", "Шипастая дубина", W, 2, 4, "Hand1", True, True, "шипаст дубин"),
    ("blessed_blade", "weapon/blessed_blade.png", "Благословенный клинок", W, 1, 3, "Hand1", False, True, "благословен клинок свят меч", True),
    ("demon_trident", "weapon/demon_trident.png", "Демонический трезубец", W, 2, 4, "Hand1", True, True, "демонич трезуб", True),
    ("demon_whip", "weapon/demon_whip.png", "Демоническая плеть", W, 2, 2, "Hand1", False, False, "демонич плеть", True),
    ("lajatang", "weapon/lajatang1.png", "Двулезвийный посох", W, 1, 4, "Hand1", True, True, "двулезвийн посох"),
    ("quickblade", "weapon/quickblade1.png", "Стремительный клинок", W, 1, 2, "Hand1", False, True, "стремител клинок"),
    ("triple_sword", "weapon/triple_sword.png", "Трёхклинковый меч", W, 2, 4, "Hand1", True, True, "трёхклинк меч"),
    ("partisan", "weapon/partisan1.png", "Протазан", W, 1, 4, "Hand1", True, True, "протазан"),
    ("sacred_scourge", "weapon/sacred_scourge.png", "Священная плеть", W, 2, 2, "Hand1", False, False, "священн плеть", True),
    ("trishula", "weapon/trishula.png", "Тришула", W, 2, 4, "Hand1", True, True, "тришул золот трезуб", True),
    ("staff_crook", "weapon/staff_mummy.png", "Посох фараона", W, 1, 4, "Hand1", True, True, "посох фараона жезл-крюк", True),
    ("long_sword_steel", "weapon/long_sword2.png", "Меч из синей стали", W, 1, 3, "Hand1", False, True, "син стал меч"),
    ("dagger_steel", "weapon/dagger2.png", "Стилет", W, 1, 2, "Hand1", False, True, "стилет мизерикорд"),
    ("mace_flanged", "weapon/mace2.png", "Пернач", W, 1, 3, "Hand1", False, True, "пернач"),
    ("orcbow", "weapon/ranged/orcbow1.png", "Орочий лук", W, 2, 4, "Hand1", True, True, "орочий лук орочь лук"),
    ("blowgun", "weapon/ranged/blowgun1.png", "Духовая трубка", W, 1, 2, "Hand1", False, True, "духов трубк"),
    ("silver_arrows", "weapon/ranged/silver_arrow1.png", "Серебряные стрелы", W, 1, 3, None, False, True, "серебрян стрел"),
    ("sling_bullets", "weapon/ranged/sling_bullet1.png", "Свинцовые пули", W, 1, 1, None, False, False, "пул свинцов"),
    ("rock", "weapon/ranged/rock.png", "Булыжник", "misc", 1, 1, None, False, False, "булыжн глыб"),
    ("triple_crossbow", "weapon/ranged/triple_crossbow.png", "Многозарядный арбалет", W, 2, 3, "Hand1", True, False, "многозаряд арбалет"),
    # ---- Посохи, жезлы, скипетры ----
    ("staff_bone", "staff/staff00.png", "Костяной посох", W, 1, 4, "Hand1", True, True, "костян посох"),
    ("staff_gold", "staff/staff02.png", "Золотой посох", W, 1, 4, "Hand1", True, True, "золот посох"),
    ("staff_blood", "staff/staff03.png", "Кровавый посох", W, 1, 4, "Hand1", True, True, "кровав посох"),
    ("staff_sun", "staff/staff04.png", "Посох солнца", W, 1, 4, "Hand1", True, True, "посох солнца солнечн посох"),
    ("staff_ember", "staff/staff06.png", "Посох углей", W, 1, 4, "Hand1", True, True, "посох угл"),
    ("staff_orb", "staff/staff07.png", "Посох со сферой", W, 1, 4, "Hand1", True, True, "посох со сферой"),
    ("staff_spiral", "staff/staff08.png", "Спиральный посох", W, 1, 4, "Hand1", True, True, "спирал посох"),
    ("staff_arcane", "staff/staff09.png", "Посох тайн", W, 1, 4, "Hand1", True, True, "посох тайн арканн посох"),
    ("wand_bone", "wand/gem_bone.png", "Костяная палочка", W, 1, 2, "Hand1", False, True, "костян палочк"),
    ("wand_fire", "wand/gem_gold.png", "Огненный жезл", W, 1, 2, "Hand1", False, True, "огнен жезл огнен палочк"),
    ("wand_void", "wand/gem_iron.png", "Жезл пустоты", W, 1, 2, "Hand1", False, True, "жезл пустот"),
    ("wand_frost", "wand/gem_silver.png", "Ледяной жезл", W, 1, 2, "Hand1", False, True, "ледян жезл ледян палочк"),
    ("wand_storm", "wand/gem_glass.png", "Грозовой жезл", W, 1, 2, "Hand1", False, True, "грозов жезл"),
    ("wand_charm", "wand/gem_ivory.png", "Жезл очарования", W, 1, 2, "Hand1", False, True, "жезл очарован"),
    ("rod_blood", "rod/rod00.png", "Кровавый скипетр", W, 1, 3, "Hand1", False, True, "кровав скипетр"),
    ("rod_royal", "rod/rod05.png", "Королевский скипетр", W, 1, 3, "Hand1", False, True, "королевск скипетр"),
    ("rod_shadow", "rod/rod07.png", "Тёмный скипетр", W, 1, 3, "Hand1", False, True, "тёмн скипетр"),
    # ---- Украшения ----
    ("amulet_amethyst", "amulet/amethyst.png", "Аметистовый амулет", "jewelry", 1, 1, "Amulet", False, False, "аметист"),
    ("amulet_jade", "amulet/jade.png", "Нефритовый амулет", "jewelry", 1, 1, "Amulet", False, False, "нефрит амулет"),
    ("amulet_pearl", "amulet/pearl.png", "Жемчужный амулет", "jewelry", 1, 1, "Amulet", False, False, "жемчуж амулет ожерель"),
    ("amulet_garnet", "amulet/garnet.png", "Гранатовый амулет", "jewelry", 1, 1, "Amulet", False, False, "гранат"),
    ("amulet_gold", "amulet/golden.png", "Золотой медальон", "jewelry", 1, 1, "Amulet", False, False, "золот медальон"),
    ("amulet_platinum", "amulet/platinum.png", "Платиновый амулет", "jewelry", 1, 1, "Amulet", False, False, "платин"),
    ("amulet_cameo", "amulet/cameo.png", "Камея", "jewelry", 1, 1, "Amulet", False, False, "каме"),
    ("amulet_filigree", "amulet/filigree.png", "Филигранный амулет", "jewelry", 1, 1, "Amulet", False, False, "филигран"),
    ("ring_jade", "ring/jade.png", "Нефритовое кольцо", "jewelry", 1, 1, "Ring1", False, False, "нефрит кольц"),
    ("ring_opal", "ring/opal.png", "Опаловое кольцо", "jewelry", 1, 1, "Ring1", False, False, "опал"),
    ("ring_coral", "ring/coral.png", "Коралловое кольцо", "jewelry", 1, 1, "Ring1", False, False, "коралл"),
    ("ring_moonstone", "ring/moonstone.png", "Кольцо с лунным камнем", "jewelry", 1, 1, "Ring1", False, False, "лунн камен"),
    ("ring_tiger", "ring/tiger_eye.png", "Кольцо с тигровым глазом", "jewelry", 1, 1, "Ring1", False, False, "тигров глаз"),
    ("ring_diamond", "ring/diamond.png", "Кольцо с бриллиантом", "jewelry", 1, 1, "Ring1", False, False, "бриллиант кольц"),
    ("ring_wood", "ring/wooden.png", "Деревянное кольцо", "jewelry", 1, 1, "Ring1", False, False, "деревян кольц"),
    ("ring_fire", "ring/randarts/fire.png", "Пламенное кольцо", "jewelry", 1, 1, "Ring1", False, False, "пламен кольц огнен кольц"),
    ("ring_ice", "ring/randarts/ice.png", "Ледяное кольцо", "jewelry", 1, 1, "Ring1", False, False, "ледян кольц"),
    ("ring_blood", "ring/randarts/blood.png", "Кровавое кольцо", "jewelry", 1, 1, "Ring1", False, False, "кровав кольц"),
    ("ring_void", "ring/randarts/dark.png", "Кольцо пустоты", "jewelry", 1, 1, "Ring1", False, False, "кольцо пустот тёмн кольц"),
    # ---- Талисманы и трофеи ----
    ("talisman_maw", "talisman/maw.png", "Талисман пасти", "valuable", 1, 1, None, False, False, "пасть"),
    ("talisman_wolf", "talisman/lupine.png", "Волчий талисман", "valuable", 1, 1, None, False, False, "волч талисман"),
    ("talisman_frost", "talisman/rimehorn.png", "Инеистый рог-талисман", "valuable", 1, 1, None, False, False, "инеист рог"),
    ("talisman_medusa", "talisman/medusa.png", "Талисман горгоны", "valuable", 1, 1, None, False, False, "горгон медуз"),
    ("talisman_bat", "talisman/vampire.png", "Талисман нетопыря", "valuable", 1, 1, None, False, False, "нетопыр летуч мыш"),
    ("talisman_storm", "talisman/storm.png", "Штормовой талисман", "valuable", 1, 1, None, False, False, "штормов талисман"),
    ("talisman_sphinx", "talisman/sphinx.png", "Талисман сфинкса", "valuable", 1, 1, None, False, False, "сфинкс"),
    ("talisman_fortress", "talisman/fortress.png", "Талисман крепости", "valuable", 1, 1, None, False, False, "крепост"),
    ("talisman_death", "talisman/death.png", "Талисман смерти", "valuable", 1, 1, None, False, False, "талисман смерти"),
    ("talisman_blade", "talisman/blade.png", "Талисман клинков", "valuable", 1, 1, None, False, False, "талисман клинк"),
    ("talisman_quill", "talisman/quill.png", "Игольчатый талисман", "valuable", 1, 1, None, False, False, "игл дикобраз"),
    ("talisman_hive", "talisman/hive.png", "Талисман улья", "valuable", 1, 1, None, False, False, "улей рой"),
    # ---- Диковины ----
    ("war_horn", "misc/misc_horn.png", "Боевой рог", "tool", 1, 2, None, False, False, "боев рог"),
    ("phantom_mirror", "misc/misc_phantom_mirror.png", "Призрачное зеркало", "tool", 1, 2, None, False, False, "призрачн зеркал"),
    ("ornate_flask", "misc/misc_bottle.png", "Узорная фляга", "tool", 1, 2, None, False, False, "фляг узорн бутыл"),
    ("void_sigil", "misc/misc_quad.png", "Печать бездны", "valuable", 1, 1, None, False, False, "печать бездны"),
    ("golden_ziggurat", "misc/misc_zigfig.png", "Золотой зиккурат", "valuable", 1, 1, None, False, False, "зиккурат пирамид"),
    ("crystal_ball", "misc/misc_crystal.png", "Хрустальный шар прорицателя", "misc", 1, 1, None, False, False, "прорицат шар"),
    ("rune_abyss", "misc/runes/rune_abyss.png", "Руна бездны", "valuable", 1, 1, None, False, False, "руна бездны"),
    ("rune_elven", "misc/runes/rune_elven.png", "Эльфийская руна", "valuable", 1, 1, None, False, False, "эльфийск рун"),
    # ---- Еда ----
    ("raw_meat", "food/chunk.png", "Сырое мясо", "food", 1, 1, None, False, False, "сыр мяс туш вырезк"),
    ("meat_ration", "food/meat_ration.png", "Мясной паёк", "food", 1, 1, None, False, False, "мясн паёк"),
    ("ration", "food/bread_ration.png", "Походный паёк", "food", 1, 1, None, False, False, "паёк провиз сухар"),
    ("banana", "food/banana.png", "Банан", "food", 1, 1, None, False, False, "банан"),
    ("lemon", "food/lemon.png", "Лимон", "food", 1, 1, None, False, False, "лимон"),
    ("apricot", "food/apricot.png", "Абрикос", "food", 1, 1, None, False, False, "абрикос персик"),
    ("bones", "food/bone3.png", "Кости", "misc", 1, 1, None, False, False, "кости скелет останк"),
    # ---- Книги и свитки ----
    ("book_purple", "book/purple.png", "Гримуар тайн", "book", 2, 2, None, False, False, "гримуар тайн"),
    ("book_silver", "book/silver.png", "Серебряный том", "book", 2, 2, None, False, False, "серебрян том"),
    ("book_cloth", "book/cloth.png", "Холщовый сборник", "book", 2, 2, None, False, False, "сборник рецепт"),
    ("book_leather", "book/leather.png", "Кожаный дневник", "book", 2, 2, None, False, False, "дневник записн"),
    ("book_turquoise", "book/turquoise.png", "Бирюзовый трактат", "book", 2, 2, None, False, False, "трактат"),
    ("book_magenta", "book/magenta.png", "Малиновый фолиант", "book", 2, 2, None, False, False, "малинов фолиант"),
    ("book_star", "book/dark_blue.png", "Звёздный атлас", "book", 2, 2, None, False, False, "звёздн атлас астролог"),
    ("scroll_purple", "scroll/scroll-purple.png", "Лиловый свиток", "scroll", 1, 1, None, False, False, "лилов свиток"),
    ("scroll_yellow", "scroll/scroll-yellow.png", "Жёлтый свиток", "scroll", 1, 1, None, False, False, "жёлт свиток"),
    ("scroll_old", "scroll/scroll-brown.png", "Старый свиток", "scroll", 1, 1, None, False, False, "стар свиток древн свиток"),
    # ---- Самоцветы ----
    ("gem_pale", "gem/crypt_found_whole.png", "Бледный самоцвет", "valuable", 1, 1, None, False, False, "бледн самоцвет лунн"),
    ("gem_pink", "gem/elf_found_whole.png", "Розовая шпинель", "valuable", 1, 1, None, False, False, "шпинел розов камен"),
    ("gem_cyan", "gem/slime_found_whole.png", "Аквамарин", "valuable", 1, 1, None, False, False, "аквамарин"),
    ("gem_aqua", "gem/vaults_found_whole.png", "Бирюза", "valuable", 1, 1, None, False, False, "бирюз"),
    ("gem_peach", "gem/zot_found_whole.png", "Солнечный камень", "valuable", 1, 1, None, False, False, "солнечн камен"),
    ("gem_amber", "gem/lair_found_whole.png", "Янтарь", "valuable", 1, 1, None, False, False, "янтар"),
]
CATALOG = CATALOG + EXTRA
PAINTED = os.path.join(ROOT, "wwwroot", "sprites", "combat-icons-loot.png")


def from_painted(index):
    """Клетка «живописного» листа 8x8, уменьшенная до 32x32; тёмная плашка фона становится прозрачной."""
    sheet = Image.open(PAINTED).convert("RGBA")
    step = sheet.width / 8
    col, row = index % 8, index // 8
    box = (round(col * step) + 10, round(row * step) + 10, round((col + 1) * step) - 10, round((row + 1) * step) - 10)
    tile = sheet.crop(box).resize((TILE, TILE), Image.LANCZOS)
    px = tile.load()
    for y in range(TILE):
        for x in range(TILE):
            r, g, b, a = px[x, y]
            if max(r, g, b) < 46:
                px[x, y] = (0, 0, 0, 0)
    return tile


def fetch(path, root=False):
    local = os.path.join(CACHE, ("rl__" if root else "") + path.replace("/", "__"))
    if not os.path.exists(local):
        os.makedirs(CACHE, exist_ok=True)
        with urllib.request.urlopen((RAW_ROOT if root else RAW) + path, timeout=30) as resp:
            data = resp.read()
        with open(local, "wb") as f:
            f.write(data)
    return Image.open(local).convert("RGBA")


def fit_tile(img):
    """Приводит тайл к 32x32 (большие ужимает с сохранением пропорций, пиксельно)."""
    if img.size == (TILE, TILE):
        return img
    img = img.copy()
    img.thumbnail((TILE, TILE), Image.NEAREST)
    canvas = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    canvas.paste(img, ((TILE - img.width) // 2, (TILE - img.height) // 2), img)
    return canvas


def from_doll(img):
    """Деталь «куклы» персонажа (пояс, факел…) лежит в углу тайла — вырезаем и центрируем,
    мелкие детали увеличиваем в целое число раз, чтобы пиксели оставались квадратными."""
    img = img.crop(img.getbbox())
    k = max(1, min(3, 28 // max(img.width, img.height)))
    if k > 1:
        img = img.resize((img.width * k, img.height * k), Image.NEAREST)
    canvas = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    canvas.paste(img, ((TILE - img.width) // 2, (TILE - img.height) // 2), img)
    return canvas


def draw_key():
    """Ключа в rltiles нет — рисуем в той же манере: тёмный контур, латунь с бликом."""
    outline, dark, mid, light = (38, 24, 8, 255), (122, 84, 22, 255), (196, 150, 52, 255), (246, 216, 120, 255)
    img = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    px = img.load()

    def put(x, y, c):
        if 0 <= x < TILE and 0 <= y < TILE:
            px[x, y] = c

    # кольцо-головка (слева сверху)
    cx, cy, r_out, r_in = 10, 10, 7, 3.2
    for y in range(TILE):
        for x in range(TILE):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if r_in <= d <= r_out:
                put(x, y, light if (x + y) < cx + cy - 3 else mid if d < r_out - 1.5 else dark)
    # стержень по диагонали к правому нижнему углу
    for t in range(0, 17):
        x, y = 14 + t, 14 + t
        for o in (-1, 0, 1):
            put(x + o, y - o, mid if o else light)
        put(x - 2, y + 2, dark)
    # бородка
    for bx, by in ((24, 28), (25, 29), (26, 30), (27, 27), (28, 28), (29, 29)):
        for o in range(3):
            put(bx - o, by, dark if o == 2 else mid)
    # контур: прозрачные пиксели рядом с непрозрачными
    solid = {(x, y) for x in range(TILE) for y in range(TILE) if px[x, y][3]}
    for x, y in solid:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in solid:
                put(x + dx, y + dy, outline)
    return img


def draw_belt():
    """Пояса в rltiles нет — кожаный ремень с латунной пряжкой в той же манере."""
    outline, d, m, l = (30, 16, 8, 255), (92, 52, 24, 255), (140, 84, 40, 255), (184, 122, 66, 255)
    bd, bm, bl = (122, 84, 22, 255), (196, 150, 52, 255), (246, 216, 120, 255)
    img = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    px = img.load()
    y0, y1 = 12, 20
    for x in range(1, 31):
        for y in range(y0, y1):
            px[x, y] = l if y == y0 else d if y == y1 - 1 else m
        if x % 4 == 2 and x > 18:
            px[x, 16] = outline  # дырочки ремня
    for x in range(1, 31):
        px[x, 13] = l if x % 2 else m  # строчка
    # пряжка
    for x in range(8, 16):
        for y in range(9, 23):
            edge = x in (8, 9, 14, 15) or y in (9, 10, 21, 22)
            if edge:
                px[x, y] = bl if (x <= 9 or y <= 10) else bm if x < 15 and y < 22 else bd
    for y in range(12, 20):
        px[11, y] = bm  # язычок
    solid = {(x, y) for x in range(TILE) for y in range(TILE) if px[x, y][3]}
    for x, y in solid:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < TILE and 0 <= ny < TILE and (nx, ny) not in solid:
                px[nx, ny] = outline
    return img


DRAWN = {"key": draw_key, "belt": draw_belt}


def main():
    legacy = Image.open(LEGACY).convert("RGBA")
    ids = [c[0] for c in CATALOG]
    assert len(ids) == len(set(ids)), "дублирующиеся id"

    rows = (len(CATALOG) + COLS - 1) // COLS
    atlas = Image.new("RGBA", (COLS * TILE, rows * TILE), (0, 0, 0, 0))
    items = []
    css = [
        "/* Пиксельные иконки предметов (сгенерировано tools/sprites/build_items.py — не править руками).",
        "   Источник: Dungeon Crawl Stone Soup rltiles (CC0 / public domain),",
        "   https://github.com/crawl/crawl/tree/master/crawl-ref/source/rltiles.",
        "   Позиции заданы в процентах — .pix можно растянуть до любого размера. */",
        ".pix {",
        "    width: 32px; height: 32px;",
        "    background-image: url('../sprites/rpg-items.png');",
        "    background-repeat: no-repeat;",
        f"    background-size: {COLS * 100}% {rows * 100}%;",
        "    image-rendering: pixelated;",
        "    display: inline-block;",
        "}",
    ]

    for i, entry in enumerate(CATALOG):
        iid, src, name, cat, w, h, slot, two, diag, kw = entry[:10]
        art = len(entry) > 10 and entry[10]
        col, row = i % COLS, i // COLS
        if src.startswith("painted:"):
            tile = from_painted(int(src[8:]))
        elif src.startswith("legacy"):
            lc, lr = LEGACY_POS[src.split(":", 1)[1] if ":" in src else iid]
            tile = legacy.crop((lc * TILE, lr * TILE, (lc + 1) * TILE, (lr + 1) * TILE))
        elif src.startswith("draw:"):
            tile = DRAWN[src[5:]]()
        elif src.startswith("@"):
            tile = from_doll(fetch(src[1:], root=True))
        else:
            tile = fit_tile(fetch(src))
        atlas.paste(tile, (col * TILE, row * TILE), tile)

        x = 0 if COLS == 1 else col * 100 / (COLS - 1)
        y = 0 if rows == 1 else row * 100 / (rows - 1)
        css.append(f".pix-{iid} {{ background-position: {x:.4g}% {y:.4g}%; }}")

        entry = {"id": iid, "name": name, "cat": cat, "w": w, "h": h}
        if slot:
            entry["slot"] = slot
        if two:
            entry["twoHanded"] = True
        if diag:
            entry["diag"] = True
        if art:
            entry["art"] = True
        if kw:
            entry["kw"] = kw.split()
        items.append(entry)

    atlas.save(os.path.join(ROOT, "wwwroot", "sprites", "rpg-items.png"), optimize=True)
    with open(os.path.join(ROOT, "wwwroot", "sprites", "items.json"), "w", encoding="utf-8") as f:
        json.dump({"cols": COLS, "rows": rows, "items": items}, f, ensure_ascii=False, indent=1)
    with open(os.path.join(ROOT, "wwwroot", "css", "pixel-items.css"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(css) + "\n")
    print(f"{len(items)} предметов, атлас {COLS}x{rows}")


if __name__ == "__main__":
    main()
