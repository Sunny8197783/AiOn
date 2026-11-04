// Item catalogue. Prices are fictional 2026 won; weights and volumes are game approximations, not advice.
// needsCheck: what turns "bought" into "usable" (batteries in, charged, tuned). risk: why an item can hurt.
// facts: FACTS ids (game/facts.js) that the item's tip is based on.
// cells: [w, h] in the bag grid (game/pack.js), 0.5 L per cell, so w*h*0.5 >= volumeL. pocket: small and flat
// enough (<= 0.2 L) for the bag's front pocket. fixture: installed at home, never packed (blackout curtain).
// charge (battery packs only): how full a device is after state.check() loads this pack, 0..100.

export const CATALOG = {
  water_2l: { name: '생수 2L', unit: '병', price: 1100, weightKg: 2.05, volumeL: 2.2, category: 'water', cells: [3, 2], facts: ['home_tub', 'water_sphere'] },
  water_500: { name: '생수 500mL', unit: '병', price: 600, weightKg: 0.52, volumeL: 0.6, category: 'water', cells: [2, 1], facts: ['bag_days', 'water_sphere'] },
  rice_instant: { name: '즉석밥', unit: '개', price: 1500, weightKg: 0.22, volumeL: 0.4, category: 'food', cells: [1, 1], facts: ['bag_nocook'] },
  canned: { name: '통조림(참치·햄)', unit: '캔', price: 2800, weightKg: 0.2, volumeL: 0.25, category: 'food', cells: [1, 1], facts: ['bag_nocook', 'home_30'] },
  ramen: { name: '라면', unit: '봉', price: 900, weightKg: 0.12, volumeL: 0.6, category: 'food', cells: [2, 1], needs: ['hot_water'], facts: ['home_30'] },
  bars: { name: '건빵·에너지바', unit: '봉', price: 1500, weightKg: 0.1, volumeL: 0.15, category: 'food', cells: [1, 1], pocket: true, facts: ['bag_nocook'] },
  radio: { name: '휴대용 라디오(건전지식)', unit: '대', price: 24000, weightKg: 0.3, volumeL: 0.6, category: 'info', cells: [2, 1], needsCheck: 'batteries', facts: ['radio_batt', 'power_prep', 'radio_listen'] },
  batteries_aa: { name: 'AA 건전지 4개', unit: '팩', price: 4500, weightKg: 0.1, volumeL: 0.05, category: 'power', cells: [1, 1], pocket: true, charge: 100, facts: ['radio_batt'] },
  flashlight: { name: '손전등(LED)', unit: '개', price: 9000, weightKg: 0.2, volumeL: 0.4, category: 'light', cells: [1, 1], needsCheck: 'batteries', facts: ['power_prep', 'candle_no'] },
  powerbank: { name: '보조배터리', unit: '개', price: 22000, weightKg: 0.22, volumeL: 0.15, category: 'power', cells: [1, 1], pocket: true, needsCheck: 'charge', facts: ['comms_battery'] },
  first_aid: { name: '구급함(상비약·밴드)', unit: '개', price: 15000, weightKg: 0.5, volumeL: 1.5, category: 'health', cells: [3, 1], facts: ['bag_days'] },
  meds: { name: '복용 중인 약(1주일분)', unit: '봉투', price: 0, weightKg: 0.1, volumeL: 0.1, category: 'health', cells: [1, 1], pocket: true, notSold: true, facts: ['contact_fields', 'people_unhcr'] },
  masks: { name: '마스크 10매', unit: '팩', price: 5000, weightKg: 0.1, volumeL: 0.4, category: 'health', cells: [1, 1], facts: [] },
  wipes: { name: '물티슈', unit: '팩', price: 2000, weightKg: 0.4, volumeL: 0.6, category: 'hygiene', cells: [2, 1], facts: [] },
  tissue: { name: '휴지', unit: '롤', price: 1500, weightKg: 0.15, volumeL: 0.8, category: 'hygiene', cells: [2, 1], facts: [] },
  trash_bags: { name: '비닐봉투·쓰레기봉투', unit: '묶음', price: 2500, weightKg: 0.2, volumeL: 0.3, category: 'hygiene', cells: [1, 1], facts: [] },
  blanket: { name: '은박 보온담요', unit: '장', price: 3000, weightKg: 0.06, volumeL: 0.2, category: 'warmth', cells: [1, 1], pocket: true, facts: ['bag_foil'] },
  raincoat: { name: '우의', unit: '벌', price: 3000, weightKg: 0.15, volumeL: 0.3, category: 'warmth', cells: [1, 1], facts: [] },
  clothes: { name: '여벌 옷·양말', unit: '벌', price: 0, weightKg: 0.6, volumeL: 3, category: 'warmth', cells: [3, 2], notSold: true, facts: ['home_clothes'] },
  hygiene_kit: { name: '세면도구', unit: '세트', price: 0, weightKg: 0.3, volumeL: 0.8, category: 'hygiene', cells: [2, 1], notSold: true, facts: [] },
  whistle: { name: '호루라기', unit: '개', price: 2000, weightKg: 0.02, volumeL: 0.02, category: 'tool', cells: [1, 1], pocket: true, facts: [] },
  tape: { name: '청테이프', unit: '개', price: 2000, weightKg: 0.2, volumeL: 0.3, category: 'tool', cells: [1, 1], facts: [] },
  water_jug: { name: '접이식 물통 10L', unit: '개', price: 7000, weightKg: 0.3, volumeL: 1, category: 'water', cells: [2, 1], facts: ['water_point'] },
  candles: { name: '양초 6개', unit: '팩', price: 3500, weightKg: 0.4, volumeL: 0.4, category: 'light', cells: [1, 1], risk: 'fire', facts: ['candle_prep', 'candle_no', 'candle_rules'] },
  gas_stove: { name: '휴대용 가스레인지', unit: '대', price: 25000, weightKg: 1.8, volumeL: 6, category: 'fire', cells: [4, 3], risk: 'fire', facts: ['home_stove', 'shelter_rules'] },
  butane: { name: '부탄가스 4개', unit: '묶음', price: 6000, weightKg: 1, volumeL: 2, category: 'fire', cells: [2, 2], risk: 'fire', facts: ['home_stove'] },
  cash: { name: '현금 5만 원', unit: '묶음', price: 50000, weightKg: 0.01, volumeL: 0.01, category: 'docs', cells: [1, 1], pocket: true, atmOnly: true, facts: ['bag_cash'] },
  // Made at home (서류·연락처 정리), not bought.
  contact_card: { name: '비상연락처 카드', unit: '장', price: 0, weightKg: 0.01, volumeL: 0.01, category: 'docs', cells: [1, 1], pocket: true, notSold: true, facts: ['contact_card', 'contact_fields', 'contact_meet'] },
  doc_copies: { name: '신분증·중요 서류 사본', unit: '묶음', price: 0, weightKg: 0.05, volumeL: 0.05, category: 'docs', cells: [1, 1], pocket: true, notSold: true, facts: ['bag_copies', 'docs_take'] },
  // The original, picked up from the wallet on the living-room table (story/world.js).
  id_card: { name: '신분증(주민등록증)', unit: '장', price: 0, weightKg: 0.01, volumeL: 0.01, category: 'docs', cells: [1, 1], pocket: true, notSold: true, facts: ['docs_take'] },
  // Already in the fridge; only safe for a limited time once the power is off.
  fridge_food: { name: '냉장고 반찬', unit: '통', price: 0, weightKg: 0.4, volumeL: 0.5, category: 'food', cells: [1, 1], notSold: true, risk: 'spoil', facts: ['food_fridge', 'food_4h', 'food_door'] },
  // Found at home: last year's bag snack (past its date), a half-used battery pack, towels, a keepsake.
  old_bars: { name: '건빵(작년 비상가방)', unit: '봉', price: 0, weightKg: 0.1, volumeL: 0.15, category: 'food', cells: [1, 1], pocket: true, notSold: true, risk: 'expired', facts: ['bag_nocook'] },
  batteries_old: { name: 'AA 건전지(쓰던 것)', unit: '팩', price: 0, weightKg: 0.1, volumeL: 0.05, category: 'power', cells: [1, 1], pocket: true, notSold: true, charge: 30, facts: ['radio_batt'] },
  towel: { name: '수건', unit: '장', price: 0, weightKg: 0.2, volumeL: 0.6, category: 'hygiene', cells: [2, 1], notSold: true, facts: ['fire_big'] },
  photo_album: { name: '가족사진 앨범', unit: '권', price: 0, weightKg: 0.5, volumeL: 1, category: 'keepsake', cells: [2, 1], notSold: true, facts: [] },
  // Hung on the living-room window (집 안 대비); counts as used once G.know.curtain is set.
  blackout_curtain: { name: '암막 커튼', unit: '장', price: 18000, weightKg: 1.2, volumeL: 4, category: 'home', fixture: true, facts: ['night_lights'] },
  cooked_meat: { name: '남은 익힌 고기', unit: '통', price: 0, weightKg: 0.3, volumeL: 0.4, category: 'food', cells: [1, 1], notSold: true, risk: 'spoil', facts: ['myth_meat', 'food_4h'] },
};

// What 정민 has at home before preparing. The flashlight has no batteries: owned is not usable.
export const START_HOME = { water_2l: 2, ramen: 5, rice_instant: 2, canned: 1, flashlight: 1, meds: 1, clothes: 2, hygiene_kit: 1, tissue: 2, fridge_food: 4, cooked_meat: 1, old_bars: 2, towel: 3, photo_album: 1 };

// A 25 L day pack; above 8 kg walking slows down (game numbers). The grid is 10 x 5 cells of 0.5 L = 25 L,
// and the front pocket holds 0.5 L more (game rule: papers in the pocket come out at once).
export const BAG_LIMIT = { weightKg: 8, volumeL: 25 };
export const BAG_GRID = { cols: 10, rows: 5, cellL: 0.5 };
export const POCKET_L = 0.5;

export const MART_SECTIONS = {
  water: { label: '생수·음료', items: ['water_2l', 'water_500'] },
  food: { label: '식품', items: ['rice_instant', 'canned', 'ramen', 'bars'] },
  daily: { label: '생활용품', items: ['wipes', 'tissue', 'trash_bags', 'tape', 'raincoat', 'blanket', 'water_jug', 'gas_stove', 'butane', 'whistle', 'blackout_curtain'] },
  elec: { label: '전지·전기용품', items: ['radio', 'batteries_aa', 'flashlight', 'powerbank', 'candles'] },
  health: { label: '의약외품', items: ['first_aid', 'masks'] },
};

// The convenience store: fewer items, a little dearer.
export const STORE_ITEMS = { water_500: 700, bars: 1800, ramen: 1100, batteries_aa: 5500, wipes: 2500, tissue: 1800, masks: 6000, candles: 4000 };

export const itemName = (id) => CATALOG[id]?.name ?? id;
// Can go in the backpack (fixtures stay at home). Bag UIs list only these.
export const packable = (id) => !!CATALOG[id] && !CATALOG[id].fixture;
