"""
Каталог портретов: wwwroot/sprites/portraits.json.

Каждой клетке каждого листа — смысловой id, название, тип существа и основы слов. Мастер (нейросеть)
выбирает портрет по id («wolf», «lich»), а харнес подбирает его сам по имени врага («Снежный волк 2»),
если модель ошиблась или не указала портрет. Раньше модель получала «classic:0..15» вслепую — отсюда
волки с портретом гоблина.

Порядок и клетки существующих записей не менять: id сохраняются в кампаниях. Новое — дописывать.
Чтобы добавить свой лист (например, сгенерированный по docs/ArtPipeline.md): положите PNG 4x4 в
wwwroot/sprites, добавьте его в SHEETS и записи в PORTRAITS, запустите скрипт и пересоберите проект.

Запуск: python tools/sprites/build_portraits.py
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

SHEETS = {
    "classic": {"file": "enemy-portraits.png", "cols": 4, "rows": 4},
    "humanoid": {"file": "enemy-portraits-humanoids.png", "cols": 4, "rows": 4},
    "monster": {"file": "enemy-portraits-monsters.png", "cols": 4, "rows": 4},
    "variants": {"file": "enemy-portraits-variants.png", "cols": 4, "rows": 4},
    "variants2": {"file": "enemy-portraits-variants2.png", "cols": 4, "rows": 4},
    "variants3": {"file": "enemy-portraits-variants3.png", "cols": 4, "rows": 4},
    "companion": {"file": "companion-portraits.png", "cols": 4, "rows": 4},
    "hero": {"file": "hero-avatars.png", "cols": 3, "rows": 2},
    # Киберпанк — нарисован кодом (tools/sprites/build_cyber_portraits.py).
    "cyber_street": {"file": "cyber-enemies-street.png", "cols": 4, "rows": 4},
    "cyber_machines": {"file": "cyber-enemies-machines.png", "cols": 4, "rows": 4},
    "cyber_persons": {"file": "cyber-persons.png", "cols": 4, "rows": 4},
    "cyber_hero": {"file": "cyber-hero-avatars.png", "cols": 3, "rows": 2},
    # Современность: основные листы имеют процедурный fallback; supernatural — отдельный живописный лист.
    "modern_street": {"file": "modern-enemies-street.png", "cols": 4, "rows": 4},
    "modern_threats": {"file": "modern-enemies-threats.png", "cols": 4, "rows": 4},
    "modern_supernatural": {"file": "modern-enemies-supernatural.png", "cols": 4, "rows": 4},
    "modern_persons": {"file": "modern-persons.png", "cols": 4, "rows": 4},
    "modern_hero": {"file": "modern-hero-avatars.png", "cols": 3, "rows": 2},
}

# Типы существ (kind) — от них зависят слабости и добыча (см. LootTables).
KINDS = {
    "humanoid": "гуманоид", "beast": "зверь", "undead": "нежить", "construct": "конструкт", "dragon": "дракон",
    "demon": "демон", "aberration": "аберрация", "elemental": "элементаль", "plant": "растение/гриб",
    "ooze": "слизь", "giant": "великан", "celestial": "небожитель", "monstrosity": "чудовище",
    # Киберпанк
    "cyborg": "киборг", "android": "андроид", "drone": "дрон", "mech": "мех/робот", "program": "программа/ИИ",
    "mutant": "мутант",
    # Современность
    "vehicle": "техника",
}

# (id, лист, клетка, название, тип, ранг-подсказка, пол, основы слов)
# ранг-подсказка: ordinary — рядовой облик, elite, boss, legend — грандиозный облик для босса арки.
# person=True — облик подходит и для героя/спутника/NPC (лицо разумного персонажа).
P = []


def add(pid, sheet, cell, title, kind, tier, words, gender="", person=False, genre=""):
    e = {"id": pid, "sheet": sheet, "cell": cell, "title": title, "kind": kind, "tier": tier,
         "gender": gender, "person": person, "kw": words.split()}
    if genre:
        e["genre"] = genre
    P.append(e)


# ---- enemy-portraits.png (бывший classic:0..15) ----
add("goblin", "classic", 0, "Гоблин с кинжалом и щитом", "humanoid", "ordinary", "гоблин хобгоблин кобольд бес-воришк goblin kobold", "m")
add("skeleton_warrior", "classic", 1, "Скелет-воин в доспехе", "undead", "ordinary", "скелет костян мертвец skeleton костяк", "m")
add("cultist", "classic", 2, "Культист в алой рясе", "humanoid", "ordinary", "культист сектант жрец послушник фанатик культ cultist", "m")
add("wolf", "classic", 3, "Волк", "beast", "ordinary", "волк волч варг пёс пес собак гончая шакал гиен лис оборот wolf warg", "")
add("giant_spider", "classic", 4, "Гигантский паук", "beast", "ordinary", "паук паучих паучь арахн тарантул spider", "")
add("bandit", "classic", 5, "Разбойник в маске", "humanoid", "ordinary", "разбойник бандит грабитель налётчик налетчик головорез вор bandit", "m", True)
add("slime", "classic", 6, "Кислотная слизь", "ooze", "ordinary", "слизь слизень желе студень ооз слайм кисел slime ooze", "")
add("wraith", "classic", 7, "Призрак-рыцарь в саване", "undead", "elite", "призрак дух привидение фантом тень умертвие wraith ghost spectre", "")
add("troll", "classic", 8, "Лесной тролль", "giant", "elite", "тролль troll", "m")
add("ogre_warlord", "classic", 9, "Огр-военачальник в латах", "giant", "elite", "огр людоед ogre военачальник", "m")
add("vampire", "classic", 10, "Вампир-аристократ", "undead", "boss", "вампир упырь кровосос граф носферату vampire", "m", True)
add("lich", "classic", 11, "Лич с посохом-черепом", "undead", "boss", "лич некромант чернокнижник колдун-мертвец lich necromancer", "m")
add("dragon_red", "classic", 12, "Красный дракон", "dragon", "legend", "дракон красн огнедыш змий dragon wyrm", "")
add("archdemon", "classic", 13, "Архидемон", "demon", "legend", "демон дьявол архидемон бес балрог инферн demon devil", "")
add("eldritch_horror", "classic", 14, "Древний ужас с глазом", "aberration", "legend", "ужас древн щупальц спрут бездн кошмар eldritch", "")
add("golem_bronze", "classic", 15, "Бронзовый голем", "construct", "boss", "голем механизм конструкт автоматон бронз golem construct", "")

# ---- enemy-portraits-humanoids.png (бывший humanoid:0..15) ----
add("thug", "humanoid", 0, "Головорез в капюшоне", "humanoid", "ordinary", "головорез громила бандит убийца наёмник наемник thug cutthroat", "m", True)
add("assassin", "humanoid", 1, "Ассасин в чёрной маске", "humanoid", "elite", "ассасин убийц ниндзя тень шпион assassin", "", True)
add("soldier", "humanoid", 2, "Солдат в шлеме-капеллине", "humanoid", "ordinary", "солдат стражник страж латник пехот караульн дозорн soldier guard", "m", True)
add("mercenary_captain", "humanoid", 3, "Капитан наёмников", "humanoid", "elite", "капитан наёмник наемник рыцарь командир сотник captain knight", "m", True)
add("pirate", "humanoid", 4, "Пират с повязкой", "humanoid", "ordinary", "пират корсар моряк флибустьер pirate", "m", True)
add("plague_doctor", "humanoid", 5, "Чумной доктор", "humanoid", "elite", "чумной доктор лекарь-маска чума plague", "m", True)
add("inquisitor", "humanoid", 6, "Инквизитор", "humanoid", "elite", "инквизитор охотник на ведьм храмовник witch-hunter inquisitor", "m", True)
add("blood_witch", "humanoid", 7, "Кровавая ведьма", "humanoid", "boss", "ведьм колдунья чародейка магесса кровав witch sorceress", "f", True)
add("dark_bishop", "humanoid", 8, "Тёмный епископ", "humanoid", "boss", "епископ кардинал жрец священник проповедник первосвящ bishop priest", "m", True)
add("berserker", "humanoid", 9, "Берсерк", "humanoid", "elite", "берсерк варвар дикар воитель berserker barbarian", "m", True)
add("duergar", "humanoid", 10, "Серый гном-колдун", "humanoid", "elite", "дуэргар серый гном подземн дварф старейшин duergar", "m", True)
add("drow_blade", "humanoid", 11, "Тёмная эльфийка с саблей", "humanoid", "elite", "дроу тёмн эльф темн эльфийк drow", "f", True)
add("goblin_alchemist", "humanoid", 12, "Гоблин-алхимик", "humanoid", "ordinary", "гоблин алхимик подрывник изобретатель бомб", "m", True)
add("orc_shaman", "humanoid", 13, "Орк-шаман", "humanoid", "elite", "орк шаман вождь знахарь orc shaman", "m", True)
add("ratfolk", "humanoid", 14, "Крысолюд с бомбой", "humanoid", "ordinary", "крыс крысолюд скавен ratfolk", "m", True)
add("lizardfolk", "humanoid", 15, "Людоящер", "humanoid", "ordinary", "ящер людоящер ящеролюд рептил lizard", "m", True)

# ---- enemy-portraits-monsters.png (бывший monster:0..15) ----
add("giant_scorpion", "monster", 0, "Гигантский скорпион", "beast", "ordinary", "скорпион scorpion", "")
add("giant_serpent", "monster", 1, "Гигантский змей", "monstrosity", "elite", "змей змея василиск наг виверна wyvern serpent basilisk", "")
add("owlbear", "monster", 2, "Совомедведь", "monstrosity", "elite", "совомедвед медвед медведь сова филин owlbear bear", "")
add("dire_boar", "monster", 3, "Лютый вепрь", "beast", "ordinary", "вепрь кабан секач свин бык тур лось boar", "")
add("drowned_zombie", "monster", 4, "Утопленник", "undead", "ordinary", "утоплен зомби мертвяк гуль болотн живой мертвец zombie ghoul", "")
add("banshee", "monster", 5, "Банши", "undead", "elite", "банши плакальщиц вопл дух женщины banshee", "f")
add("mummy_lord", "monster", 6, "Мумия-фараон", "undead", "boss", "мумия фараон гробниц mummy", "m")
add("bone_hydra", "monster", 7, "Костяная гидра", "undead", "legend", "гидра костян дракол драколич hydra", "")
add("mimic", "monster", 8, "Мимик-сундук", "monstrosity", "ordinary", "мимик сундук ларец mimic", "")
add("black_knight", "monster", 9, "Чёрный рыцарь", "undead", "boss", "чёрн рыцарь черн рыцарь рыцарь смерти паладин падший тёмный рыцарь death knight", "m")
add("crystal_golem", "monster", 10, "Кристальный голем", "elemental", "elite", "кристал хрустал самоцвет голем льдист crystal", "")
add("myconid", "monster", 11, "Грибной ужас", "plant", "elite", "гриб грибн миконид плесень спор fungus myconid", "")
add("storm_elemental", "monster", 12, "Грозовой элементаль", "elemental", "elite", "буря вихрь смерч гроз элементаль воздух молни storm elemental", "")
add("frost_giant", "monster", 13, "Ледяной великан", "giant", "boss", "великан инеист йотун исполин циклоп giant", "m")
add("fallen_angel", "monster", 14, "Падший ангел", "celestial", "legend", "ангел серафим падш небожител angel", "m")
add("elder_eye", "monster", 15, "Многоглазое древнее нечто", "aberration", "legend", "глаз созерцат бехолдер многоглаз щупальц beholder", "")

# ---- вариации (tools/sprites/build_variants.py, строго в том же порядке) ----
V = [
    ("dragon_green", "Зелёный дракон", "dragon", "legend", "зелён дракон ядовит болотн дракон"),
    ("dragon_blue", "Синий дракон", "dragon", "legend", "син дракон грозов дракон морск дракон"),
    ("dragon_black", "Чёрный дракон", "dragon", "legend", "чёрн дракон черн дракон теневой дракон"),
    ("dragon_white", "Белый дракон", "dragon", "legend", "бел дракон ледян дракон снежн дракон морозн дракон"),
    ("dragon_gold", "Золотой дракон", "dragon", "legend", "золот дракон солнечн дракон"),
    ("slime_blue", "Ледяная слизь", "ooze", "ordinary", "ледян слизь син слизь водян слизь"),
    ("slime_red", "Кровавая слизь", "ooze", "ordinary", "кровав слизь красн слизь магм слизь"),
    ("slime_violet", "Чёрная слизь бездны", "ooze", "elite", "фиолет слизь чёрн слизь черн слизь пурпурн слизь"),
    ("slime_amber", "Янтарная слизь", "ooze", "ordinary", "янтарн слизь бур слизь грязев слизь"),
    ("wolf_snow", "Снежный волк", "beast", "ordinary", "снежн волк белый волк бел волк ледян волк зимн волк"),
    ("hellhound", "Адская гончая", "demon", "elite", "адск гончая адск пёс огнен волк огнен пес инферн гонча hellhound"),
    ("spider_venom", "Ядовитый паук", "beast", "ordinary", "ядовит паук зелён паук болотн паук"),
    ("spider_frost", "Ледяной паук", "beast", "ordinary", "ледян паук син паук морозн паук"),
    ("wraith_green", "Чумной призрак", "undead", "elite", "зелён призрак чумн призрак болотн дух ядовит призрак"),
    ("wraith_crimson", "Кровавый призрак", "undead", "elite", "кровав призрак красн призрак багров дух"),
    ("demon_frost", "Ледяной демон", "demon", "legend", "ледян демон морозн демон"),
    ("demon_void", "Демон бездны", "demon", "legend", "демон бездн пустот демон тёмн демон"),
    ("horror_green", "Болотный ужас", "aberration", "legend", "болотн ужас зелён ужас трясин"),
    ("golem_iron", "Железный голем", "construct", "boss", "железн голем стальн голем чугун"),
    ("troll_cave", "Пещерный тролль", "giant", "elite", "пещерн тролль серый тролль камен тролль горн тролль"),
    ("troll_frost", "Ледяной тролль", "giant", "elite", "ледян тролль снежн тролль"),
    ("lich_frost", "Ледяной лич", "undead", "boss", "ледян лич морозн некромант"),
    ("lich_plague", "Чумной лич", "undead", "boss", "чумн лич гнил некромант мор"),
    ("fire_giant", "Огненный великан", "giant", "boss", "огнен великан огнен исполин"),
    ("fire_elemental", "Огненный вихрь", "elemental", "elite", "огнен элементаль огнен вихрь пламен элементаль огня пламя"),
    ("toxic_elemental", "Ядовитый смерч", "elemental", "elite", "ядовит смерч ядовит элементаль миазм"),
    ("crystal_ruby", "Рубиновый голем", "elemental", "elite", "рубин голем красн кристал"),
    ("crystal_amethyst", "Аметистовый голем", "elemental", "elite", "аметист голем фиолет кристал"),
    ("crystal_emerald", "Изумрудный голем", "elemental", "elite", "изумруд голем зелён кристал"),
    ("scorpion_black", "Чёрный скорпион", "beast", "ordinary", "чёрн скорпион черн скорпион пещерн скорпион"),
    ("scorpion_fire", "Огненный скорпион", "beast", "elite", "огнен скорпион красн скорпион"),
    ("serpent_sea", "Морской змей", "monstrosity", "boss", "морск змей левиафан водян змей"),
    ("serpent_crimson", "Багровый змей", "monstrosity", "elite", "багров змей красн змей кровав змей"),
    ("banshee_green", "Болотная банши", "undead", "elite", "болотн банши зелён банши"),
    ("myconid_violet", "Лиловый грибной ужас", "plant", "elite", "лилов гриб фиолет гриб ядовит гриб"),
    ("myconid_glow", "Светящийся грибной ужас", "plant", "elite", "светящ гриб голуб гриб пещерн гриб"),
    ("eldritch_green", "Изумрудное око бездны", "aberration", "legend", "зелён глаз изумрудн око"),
    ("salamander", "Огненный людоящер", "humanoid", "elite", "саламандр огнен ящер красн ящер"),
    ("orc_grey", "Серый орк-шаман", "humanoid", "elite", "серый орк гнил орк орк-шаман"),
    ("goblin_blue", "Пещерный гоблин", "humanoid", "ordinary", "пещерн гоблин син гоблин горн гоблин"),
]
for i, (vid, title, kind, tier, words) in enumerate(V):
    sheet = "variants" if i < 16 else "variants2" if i < 32 else "variants3"
    add(vid, sheet, i % 16, title, kind, tier, words)

# ---- companion-portraits.png (спутники; прежние архетипы human-male, elf-female… — алиасы) ----
C = [
    ("human-male", "Человек-воин", "m", "человек воин рыцарь боец страж паладин"),
    ("elf-female", "Эльфийка-лучница", "f", "эльф лучн следопыт"),
    ("darkelf-female", "Тёмная эльфийка", "f", "дроу тёмн эльф темн эльф"),
    ("dwarf-male", "Дварф", "m", "дварф гном дворф"),
    ("orc-male", "Орк", "m", "орк полуорк"),
    ("tiefling-female", "Тифлинг", "f", "тифлинг демоническ"),
    ("human-female-cleric", "Жрица", "f", "человек жриц жрец целител монахин клирик"),
    ("human-male-rogue", "Плут в капюшоне", "m", "человек плут вор следопыт охотник разведч ассасин"),
    ("dragonborn-male", "Драконорождённый", "m", "драконорожд дракон"),
    ("halfling-male", "Полурослик", "m", "полурослик хоббит"),
    ("catfolk-female", "Кошколюдка", "f", "кошк табакс зверолюд"),
    ("goblin-male", "Гоблин-спутник", "m", "гоблин"),
    ("human-female-mage", "Огненная чародейка", "f", "человек чарод маг волшеб колдун ведьм пиромант"),
    ("elf-male", "Эльф-друид", "m", "эльф друид следопыт"),
    ("darkelf-male", "Тёмный эльф-некромант", "m", "дроу тёмн эльф темн эльф некромант"),
    ("human-female-warrior", "Воительница", "f", "человек воин воительниц наёмниц наемниц паладин страж"),
]
for i, (cid, title, g, words) in enumerate(C):
    add(cid, "companion", i, title, "humanoid", "ordinary", words, g, True)

# ---- hero-avatars.png (3x2: воин, ловкач, маг; мужчины/женщины) ----
H = [("warrior-male", "Воин", "m", "человек воин боец рыцарь варвар"), ("rogue-male", "Ловкач", "m", "человек следопыт лучник охотник"),
     ("mage-male", "Маг", "m", "человек маг волшеб чарод колдун старец"),
     ("warrior-female", "Воительница", "f", "человек воин боец рыцарь варвар"), ("rogue-female", "Ловкачка", "f", "человек следопыт лучн охотниц плут"),
     ("mage-female", "Волшебница", "f", "человек маг волшеб чарод колдун жриц")]
for i, (hid, title, g, words) in enumerate(H):
    add(hid, "hero", i, title, "humanoid", "ordinary", words, g, True)

# ======================= Киберпанк =======================
CY = "cyberpunk"

# ---- cyber-enemies-street.png: люди улиц и корпораций ----
CS = [
    ("ganger", "Ганкер с тату", "humanoid", "ordinary", "m", True, "ганкер бандит гопник уличн налётчик налетчик шпан ganger"),
    ("ganger_f", "Бандитка с розовым каре", "humanoid", "ordinary", "f", True, "бандитк ганкерш девушк-банд"),
    ("punk", "Панк с ирокезом", "humanoid", "ordinary", "m", True, "панк ирокез punk"),
    ("bruiser", "Громила с железной челюстью", "cyborg", "ordinary", "m", False, "громил вышибал головорез бугай качок thug bruiser"),
    ("corp_guard", "Корпоративный охранник в шлеме", "humanoid", "ordinary", "m", False, "охранник охран корпоратив страж секьюрити guard security"),
    ("corp_soldier", "Корпоративный штурмовик", "humanoid", "elite", "m", False, "корпоративн штурмовик солдат спецназ боец оперативник soldier"),
    ("cop", "Полицейский в кепи", "humanoid", "ordinary", "m", True, "полицейск коп патрульн офицер сержант cop police"),
    ("netrunner", "Нетраннерша в капюшоне", "humanoid", "elite", "f", True, "нетраннер хакер взломщик netrunner hacker"),
    ("cyberpsycho", "Киберпсих в хроме", "cyborg", "boss", "m", False, "киберпсих психопат безумец маньяк cyberpsycho"),
    ("cyber_ninja", "Киберниндзя с катаной", "cyborg", "elite", "m", False, "ниндзя ассасин убийц ninja assassin"),
    ("mercenary", "Наёмник-ветеран с кибер-глазом", "humanoid", "elite", "m", True, "наёмник наемник ветеран солдат удачи merc mercenary"),
    ("sniper", "Снайперша", "humanoid", "elite", "f", True, "снайпер стрелок sniper"),
    ("tech_priest", "Техножрец", "cyborg", "elite", "m", False, "техножрец культист сектант жрец машин фанатик cultist"),
    ("raider", "Рейдер Пустошей", "humanoid", "ordinary", "m", True, "рейдер кочевник пустош мародёр мародер байкер raider"),
    ("fixer_boss", "Фиксер-босс в очках", "humanoid", "boss", "m", True, "фиксер босс главарь пахан мафиоз барон король fixer boss"),
    ("corp_exec", "Директор корпорации", "humanoid", "legend", "f", True, "директор корпорат топ-менеджер глава совет исполнительн exec ceo"),
]
for i, (pid, title, kind, tier, g, person, words) in enumerate(CS):
    add(pid, "cyber_street", i, title, kind, tier, words, g, person, CY)

# ---- cyber-enemies-machines.png: машины, программы, мутанты ----
CM = [
    ("drone_sec", "Охранный дрон", "drone", "ordinary", "дрон охранн камер наблюден drone"),
    ("drone_combat", "Боевой квадрокоптер", "drone", "elite", "квадрокоптер штурмов боевой дрон"),
    ("turret", "Автоматическая турель", "mech", "ordinary", "турел пулемётн точк турель turret"),
    ("combat_robot", "Боевой робот", "mech", "elite", "робот боевой автомат-солдат robot"),
    ("android", "Андроид", "android", "ordinary", "андроид синтетик репликант android"),
    ("mech", "Шагающий мех", "mech", "elite", "мех шагоход экзоробот погрузчик mech"),
    ("cyberdog", "Киберпёс", "beast", "ordinary", "киберпёс киберпес собак пёс пес гончая доберман волк cyberdog"),
    ("mutant_rat", "Крыса-мутант", "beast", "ordinary", "крыса-мутант крыс мутант-крыс rat"),
    ("mutant", "Мутант", "mutant", "elite", "мутант урод выродок отродье mutant"),
    ("ai_ghost", "Сетевой призрак", "program", "elite", "призрак сетев ии искусственн дух аватар ghost ai"),
    ("ice_daemon", "Демон-защитник (ICE)", "program", "boss", "лёд лед айс демон защит программ файрвол ice daemon"),
    ("spider_bot", "Паук-робот", "drone", "elite", "паук-робот паук спайдер spider"),
    ("borg_titan", "Киборг-титан", "cyborg", "boss", "титан киборг-гигант гигант тяжёл киборг cyborg"),
    ("war_mech", "Военный мех", "mech", "legend", "военн мех танк шагоход боевая машин war"),
    ("ai_core", "Ядро сверхразума", "program", "legend", "ядро сверхразум суперкомпьютер ии-бог core"),
    ("android_assassin", "Андроид-убийца", "android", "boss", "андроид-убийц синтетическ убийц ликвидатор"),
]
for i, (pid, title, kind, tier, words) in enumerate(CM):
    add(pid, "cyber_machines", i, title, kind, tier, words, "", False, CY)

# ---- cyber-persons.png: спутники и NPC ----
CP = [
    ("cy-solo-m", "Соло с кибер-глазом", "m", "человек соло наёмник наемник боец телохранител"),
    ("cy-solo-f", "Соло с тату", "f", "человек соло наёмниц наемниц бойц"),
    ("cy-netrunner-f", "Нетраннерша с визором", "f", "человек нетраннер хакер"),
    ("cy-netrunner-m", "Нетраннер в очках", "m", "человек нетраннер хакер"),
    ("cy-techie-m", "Техник в очках", "m", "человек техник инженер механик дронщик"),
    ("cy-techie-f", "Техничка", "f", "человек техник инженер механик"),
    ("cy-medtech-f", "Медтех", "f", "человек медтех медик врач риппердок медсестр"),
    ("cy-medtech-m", "Медтех с визором", "m", "человек медтех медик врач риппердок"),
    ("cy-nomad-m", "Кочевник", "m", "человек кочевник байкер водител контрабандист"),
    ("cy-nomad-f", "Кочевница", "f", "человек кочевниц байкерш водител"),
    ("cy-fixer-m", "Фиксер", "m", "человек фиксер посредник делец"),
    ("cy-corpo-f", "Корпоратка", "f", "человек корпорат менеджер агент шпион"),
    ("cy-rocker-m", "Рокер-киборг", "m", "человек рокер музыкант киборг"),
    ("cy-bartender-f", "Барменша", "f", "человек бармен барменш официантк"),
    ("cy-cop-m", "Коп", "m", "человек коп полицейск детектив"),
    ("cy-punk-f", "Панкушка", "f", "человек панк уличн"),
]
for i, (pid, title, g, words) in enumerate(CP):
    add(pid, "cyber_persons", i, title, "humanoid", "ordinary", words, g, True, CY)

# ---- cyber-hero-avatars.png (3x2: соло, раннер, нетраннер; мужчины/женщины) ----
CH = [("cy-hero-solo-m", "Соло", "m", "человек соло наёмник боец"), ("cy-hero-runner-m", "Раннер", "m", "человек раннер вор ниндзя разведч"),
      ("cy-hero-netrunner-m", "Нетраннер", "m", "человек нетраннер хакер техник"),
      ("cy-hero-solo-f", "Соло", "f", "человек соло наёмниц бойц"), ("cy-hero-runner-f", "Раннерша", "f", "человек раннер воровк разведч"),
      ("cy-hero-netrunner-f", "Нетраннерша", "f", "человек нетраннер хакер техник")]
for i, (hid, title, g, words) in enumerate(CH):
    add(hid, "cyber_hero", i, title, "humanoid", "ordinary", words, g, True, CY)


# ======================= Современность =======================
MD = "modern"

MS = [
    ("gopnik", "Гопник в спортивном костюме", "humanoid", "ordinary", "m", True, "гопник шпан хулиган пацан урка gopnik"),
    ("robber", "Грабитель в балаклаве", "humanoid", "ordinary", "m", False, "грабител налётчик налетчик балаклав маск robber"),
    ("biker", "Байкер с банданой", "humanoid", "ordinary", "m", True, "байкер мотоциклист biker"),
    ("dealer", "Барыга в капюшоне", "humanoid", "ordinary", "m", True, "барыг дилер торгаш закладчик dealer"),
    ("enforcer", "Бритый громила", "humanoid", "elite", "m", True, "громил бык вышибал бандит браток боевик enforcer bandit"),
    ("hitman", "Киллер в тёмных очках", "humanoid", "elite", "m", True, "киллер убийц наёмный убийц hitman"),
    ("sec_guard", "Охранник", "humanoid", "ordinary", "m", True, "охранник охран чоп секьюрити guard"),
    ("police_officer", "Полицейский", "humanoid", "ordinary", "m", True, "полицейск мент патрульн участков сержант police cop"),
    ("swat_officer", "Боец спецназа", "humanoid", "elite", "m", False, "спецназ омон собр штурмовик swat"),
    ("pmc_operator", "Боец ЧВК", "humanoid", "elite", "m", True, "боец чвк чвк наёмник наемник оператор контрактник pmc merc"),
    ("army_soldier", "Солдат", "humanoid", "ordinary", "m", True, "солдат военн рядов боец армии soldier"),
    ("marksman", "Снайперша", "humanoid", "elite", "f", True, "снайпер стрелок marksman"),
    ("mob_boss", "Мафиозный босс", "humanoid", "boss", "m", True, "босс мафи авторитет вор в законе главар пахан boss"),
    ("kingpin", "Король наркотрафика", "humanoid", "legend", "m", True, "наркобарон картел король барон kingpin"),
    ("corrupt_official", "Продажный чиновник", "humanoid", "boss", "m", True, "чиновник депутат мэр полковник продажн official"),
    ("prisoner", "Беглый зэк", "humanoid", "ordinary", "m", True, "зэк заключённ заключен беглец уголовник prisoner"),
]
for i, (pid, title, kind, tier, g, person, words) in enumerate(MS):
    add(pid, "modern_street", i, title, kind, tier, words, g, person, MD)

MT = [
    ("guard_dog", "Сторожевой пёс", "beast", "ordinary", "", False, "пёс пес собак ротвейлер сторожев овчарк dog"),
    ("doberman", "Доберман", "beast", "ordinary", "", False, "доберман бойцов собак питбул"),
    ("timber_wolf", "Волк", "beast", "ordinary", "", False, "волк волч wolf"),
    ("brown_bear", "Бурый медведь", "beast", "elite", "", False, "медвед медведиц шатун bear"),
    ("wild_boar", "Кабан", "beast", "ordinary", "", False, "кабан вепрь секач boar"),
    ("armored_suv", "Бронированный внедорожник", "vehicle", "elite", "", False, "внедорожник джип машин броневик бронированн suv"),
    ("technical", "Пикап с пулемётом", "vehicle", "elite", "", False, "пикап тачанк пулемёт пулемет technical"),
    ("police_car", "Полицейская машина", "vehicle", "ordinary", "", False, "полицейск машин патрульн машин мигалк"),
    ("helicopter", "Вертолёт", "vehicle", "boss", "", False, "вертолёт вертолет вертушк helicopter"),
    ("quad_drone", "Квадрокоптер", "drone", "ordinary", "", False, "квадрокоптер дрон беспилотник drone"),
    ("maniac", "Маньяк в маске", "humanoid", "boss", "m", False, "маньяк психопат убийца в маске maniac"),
    ("sect_leader", "Лидер секты", "humanoid", "boss", "m", True, "сект культ пророк гуру лидер секты"),
    ("killer_f", "Наёмная убийца", "humanoid", "elite", "f", True, "наёмная убийца убийц киллерш наёмниц наемниц"),
    ("heavy_gunner", "Пулемётчик", "humanoid", "elite", "m", False, "пулемётчик пулеметчик тяжёл тяжел гранатомётчик"),
    ("riot_cop", "Боец ОМОНа", "humanoid", "ordinary", "m", False, "омон росгвард щит оцеплен"),
    ("warlord", "Полевой командир", "humanoid", "legend", "m", True, "полевой командир генерал командир боевиков warlord"),
]
for i, (pid, title, kind, tier, g, person, words) in enumerate(MT):
    add(pid, "modern_threats", i, title, kind, tier, words, g, person, MD)

MN = [
    ("md_restless_ghost", "Беспокойный призрак", "undead", "ordinary", "призрак дух привидение фантом мертвец ghost spirit phantom"),
    ("md_possessed", "Одержимый", "humanoid", "ordinary", "одержим бесноват вселение демон внутри possessed"),
    ("md_feral_vampire", "Одичавший вампир", "undead", "elite", "вампир кровосос упырь вурдалак feral vampire"),
    ("md_vampire_aristocrat", "Вампир-аристократ", "undead", "boss", "вампир аристократ древний князь госпожа vampire aristocrat"),
    ("md_urban_werewolf", "Городской оборотень", "monstrosity", "elite", "оборотень вервольф волколак ликантроп werewolf lycan"),
    ("md_occult_cultist", "Оккультный культист", "humanoid", "ordinary", "культист сектант оккультист ритуалист cultist occult"),
    ("md_revenant", "Ревенант", "undead", "elite", "ревенант оживший мертвец зомби ходячий труп revenant zombie"),
    ("md_banshee", "Банши", "undead", "boss", "банши плакальщица кричащий дух призрак banshee wraith"),
    ("md_shadow_demon", "Теневой демон", "demon", "elite", "теневой демон бес тень дым мрак shadow demon"),
    ("md_horned_demon", "Рогатый демон", "demon", "boss", "рогатый демон дьявол исчадие сатир demon devil fiend"),
    ("md_living_mannequin", "Живой манекен", "construct", "ordinary", "живой манекен кукла автомат оживший предмет mannequin doll construct"),
    ("md_concrete_gargoyle", "Бетонная горгулья", "construct", "elite", "горгулья каменный демон статуя бетон gargoyle statue"),
    ("md_sewer_aberration", "Канализационная тварь", "aberration", "ordinary", "канализационная тварь мутант многоглазый монстр sewer aberration creature"),
    ("md_eldritch_anomaly", "Потусторонняя аномалия", "aberration", "boss", "аномалия разлом искажение пустота потусторонний ужас eldritch anomaly rift"),
    ("md_fire_elemental", "Огненный элементаль", "elemental", "elite", "огненный элементаль дух огня пламя пожар fire elemental"),
    ("md_urban_celestial", "Городской небожитель", "celestial", "legend", "небожитель ангел серафим сияющий посланник celestial angel seraph"),
]
for i, (pid, title, kind, tier, words) in enumerate(MN):
    add(pid, "modern_supernatural", i, title, kind, tier, words, "", False, MD)

MP = [
    ("md-soldier-m", "Солдат", "m", "человек солдат военн контрактник боец десантник"),
    ("md-soldier-f", "Военная", "f", "человек солдат военн контрактниц"),
    ("md-medic-f", "Медсестра", "f", "человек медсестр медик врач фельдшер"),
    ("md-medic-m", "Врач", "m", "человек врач медик хирург фельдшер"),
    ("md-hacker-m", "Хакер", "m", "человек хакер айтишник программист"),
    ("md-hacker-f", "Хакерша", "f", "человек хакер айтишниц программист"),
    ("md-detective-m", "Детектив", "m", "человек детектив сыщик следовател"),
    ("md-journalist-f", "Журналистка", "f", "человек журналист репортёр репортер блогер"),
    ("md-driver-m", "Водитель", "m", "человек водител дальнобойщик таксист"),
    ("md-mechanic-f", "Механик", "f", "человек механик технар"),
    ("md-cop-m", "Полицейский", "m", "человек полицейск мент опер"),
    ("md-lawyer-f", "Адвокат", "f", "человек адвокат юрист бизнесвумен"),
    ("md-boxer-m", "Боксёр", "m", "человек боксёр боксер боец спортсмен"),
    ("md-bartender-f", "Барменша", "f", "человек бармен официантк"),
    ("md-veteran-m", "Ветеран", "m", "человек ветеран старик отставник"),
    ("md-student-f", "Студентка", "f", "человек студент девушк"),
]
for i, (pid, title, g, words) in enumerate(MP):
    add(pid, "modern_persons", i, title, "humanoid", "ordinary", words, g, True, MD)

MH = [("md-hero-fighter-m", "Боец", "m", "человек боец солдат наёмник"), ("md-hero-rogue-m", "Ловкач", "m", "человек вор ловкач"),
      ("md-hero-expert-m", "Специалист", "m", "человек специалист детектив хакер медик"),
      ("md-hero-fighter-f", "Боец", "f", "человек боец солдат наёмниц"), ("md-hero-rogue-f", "Ловкачка", "f", "человек воровк ловкач"),
      ("md-hero-expert-f", "Специалистка", "f", "человек специалист детектив хакер медик")]
for i, (hid, title, g, words) in enumerate(MH):
    add(hid, "modern_hero", i, title, "humanoid", "ordinary", words, g, True, MD)

def main():
    ids = [p["id"] for p in P]
    assert len(ids) == len(set(ids)), "дублирующиеся id"
    sheet_cells = [(p["sheet"], p["cell"]) for p in P]
    assert len(sheet_cells) == len(set(sheet_cells)), "две записи указывают на одну клетку листа"
    for portrait in P:
        assert portrait["sheet"] in SHEETS, f"неизвестный лист: {portrait['sheet']}"
        sheet = SHEETS[portrait["sheet"]]
        assert 0 <= portrait["cell"] < sheet["cols"] * sheet["rows"], f"клетка вне листа: {portrait['id']}"
        assert portrait["kind"] in KINDS, f"неизвестный тип: {portrait['id']}={portrait['kind']}"
        assert portrait["tier"] in {"ordinary", "elite", "boss", "legend"}, f"неизвестная угроза: {portrait['id']}={portrait['tier']}"
        assert portrait["gender"] in {"", "m", "f"}, f"неизвестный пол: {portrait['id']}={portrait['gender']}"
        assert not portrait["person"] or portrait.get("genre", "") not in {CY, MD} or portrait["gender"] in {"m", "f"}, f"у портрета персонажа не указан пол: {portrait['id']}"

    # Название каждой современной/киберпанковой угрозы должно лучше всего узнавать собственный id.
    # Иначе автоподбор может заменить явно верный портрет похожим («боец ЧВК» → обычный солдат).
    person_sheets = {"cyber_persons", "cyber_hero", "modern_persons", "modern_hero"}
    norm = lambda value: value.lower().replace("ё", "е")
    for genre in (CY, MD):
        enemies = [p for p in P if p.get("genre", "") == genre and p["sheet"] not in person_sheets]
        for portrait in enemies:
            title = norm(portrait["title"])
            def score(candidate):
                return sum(len(word) for word in {norm(k) for k in candidate["kw"]} if len(word) > 1 and word in title)
            own = score(portrait)
            best = max(enemies, key=score)
            assert own >= score(best), f"название {portrait['id']} лучше совпадает с {best['id']} ({own} < {score(best)})"
    out = {"sheets": SHEETS, "kinds": KINDS, "portraits": P}
    path = os.path.join(ROOT, "wwwroot", "sprites", "portraits.json")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print(f"{len(P)} портретов")


if __name__ == "__main__":
    main()
