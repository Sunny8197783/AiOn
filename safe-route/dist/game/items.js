// Item catalogue. Prices are fictional 2026 won; weights and volumes are game approximations, not advice.
// needsCheck: what turns "bought" into "usable" (batteries in, charged, tuned). risk: why an item can hurt.
// facts: FACTS ids (game/facts.js) that the item's tip is based on.

export const CATALOG = {
  water_2l: { name: '생수 2L', unit: '병', price: 1100, weightKg: 2.05, volumeL: 2.2, category: 'water', facts: [] },
  water_500: { name: '생수 500mL', unit: '병', price: 600, weightKg: 0.52, volumeL: 0.6, category: 'water', facts: [] },
  rice_instant: { name: '즉석밥', unit: '개', price: 1500, weightKg: 0.22, volumeL: 0.4, category: 'food', facts: [] },
  canned: { name: '통조림(참치·햄)', unit: '캔', price: 2800, weightKg: 0.2, volumeL: 0.25, category: 'food', facts: [] },
  ramen: { name: '라면', unit: '봉', price: 900, weightKg: 0.12, volumeL: 0.6, category: 'food', needs: ['hot_water'], facts: [] },
  bars: { name: '건빵·에너지바', unit: '봉', price: 1500, weightKg: 0.1, volumeL: 0.15, category: 'food', facts: [] },
  radio: { name: '휴대용 라디오(건전지식)', unit: '대', price: 24000, weightKg: 0.3, volumeL: 0.6, category: 'info', needsCheck: 'batteries', facts: [] },
  batteries_aa: { name: 'AA 건전지 4개', unit: '팩', price: 4500, weightKg: 0.1, volumeL: 0.05, category: 'power', facts: [] },
  flashlight: { name: '손전등(LED)', unit: '개', price: 9000, weightKg: 0.2, volumeL: 0.4, category: 'light', needsCheck: 'batteries', facts: [] },
  powerbank: { name: '보조배터리', unit: '개', price: 22000, weightKg: 0.22, volumeL: 0.15, category: 'power', needsCheck: 'charge', facts: [] },
  first_aid: { name: '구급함(상비약·밴드)', unit: '개', price: 15000, weightKg: 0.5, volumeL: 1.5, category: 'health', facts: [] },
  meds: { name: '복용 중인 약(1주일분)', unit: '봉투', price: 0, weightKg: 0.1, volumeL: 0.1, category: 'health', notSold: true, facts: [] },
  masks: { name: '마스크 10매', unit: '팩', price: 5000, weightKg: 0.1, volumeL: 0.4, category: 'health', facts: [] },
  wipes: { name: '물티슈', unit: '팩', price: 2000, weightKg: 0.4, volumeL: 0.6, category: 'hygiene', facts: [] },
  tissue: { name: '휴지', unit: '롤', price: 1500, weightKg: 0.15, volumeL: 0.8, category: 'hygiene', facts: [] },
  trash_bags: { name: '비닐봉투·쓰레기봉투', unit: '묶음', price: 2500, weightKg: 0.2, volumeL: 0.3, category: 'hygiene', facts: [] },
  blanket: { name: '은박 보온담요', unit: '장', price: 3000, weightKg: 0.06, volumeL: 0.2, category: 'warmth', facts: [] },
  raincoat: { name: '우의', unit: '벌', price: 3000, weightKg: 0.15, volumeL: 0.3, category: 'warmth', facts: [] },
  clothes: { name: '여벌 옷·양말', unit: '벌', price: 0, weightKg: 0.6, volumeL: 3, category: 'warmth', notSold: true, facts: [] },
  hygiene_kit: { name: '세면도구', unit: '세트', price: 0, weightKg: 0.3, volumeL: 0.8, category: 'hygiene', notSold: true, facts: [] },
  whistle: { name: '호루라기', unit: '개', price: 2000, weightKg: 0.02, volumeL: 0.02, category: 'tool', facts: [] },
  tape: { name: '청테이프', unit: '개', price: 2000, weightKg: 0.2, volumeL: 0.3, category: 'tool', facts: [] },
  water_jug: { name: '접이식 물통 10L', unit: '개', price: 7000, weightKg: 0.3, volumeL: 1, category: 'water', facts: [] },
  candles: { name: '양초 6개', unit: '팩', price: 3500, weightKg: 0.4, volumeL: 0.4, category: 'light', risk: 'fire', facts: [] },
  gas_stove: { name: '휴대용 가스레인지', unit: '대', price: 25000, weightKg: 1.8, volumeL: 6, category: 'fire', risk: 'fire', facts: [] },
  butane: { name: '부탄가스 4개', unit: '묶음', price: 6000, weightKg: 1, volumeL: 2, category: 'fire', risk: 'fire', facts: [] },
  // Made at home (서류·연락처 정리), not bought.
  contact_card: { name: '비상연락처 카드', unit: '장', price: 0, weightKg: 0.01, volumeL: 0.01, category: 'docs', notSold: true, facts: [] },
  doc_copies: { name: '신분증·중요 서류 사본', unit: '묶음', price: 0, weightKg: 0.05, volumeL: 0.05, category: 'docs', notSold: true, facts: [] },
  // Already in the fridge; only safe for a limited time once the power is off.
  fridge_food: { name: '냉장고 반찬', unit: '통', price: 0, weightKg: 0.4, volumeL: 0.5, category: 'food', notSold: true, risk: 'spoil', facts: [] },
  cooked_meat: { name: '남은 익힌 고기', unit: '통', price: 0, weightKg: 0.3, volumeL: 0.4, category: 'food', notSold: true, risk: 'spoil', facts: [] },
};

// What 정민 has at home before preparing. The flashlight has no batteries: owned is not usable.
export const START_HOME = { water_2l: 2, ramen: 5, rice_instant: 2, canned: 1, flashlight: 1, meds: 1, clothes: 2, hygiene_kit: 1, tissue: 2, fridge_food: 4, cooked_meat: 1 };

// A 25 L day pack; above 8 kg walking slows down (game numbers).
export const BAG_LIMIT = { weightKg: 8, volumeL: 25 };

export const MART_SECTIONS = {
  water: { label: '생수·음료', items: ['water_2l', 'water_500'] },
  food: { label: '식품', items: ['rice_instant', 'canned', 'ramen', 'bars'] },
  daily: { label: '생활용품', items: ['wipes', 'tissue', 'trash_bags', 'tape', 'raincoat', 'blanket', 'water_jug', 'gas_stove', 'butane', 'whistle'] },
  elec: { label: '전지·전기용품', items: ['radio', 'batteries_aa', 'flashlight', 'powerbank', 'candles'] },
  health: { label: '의약외품', items: ['first_aid', 'masks'] },
};

// The convenience store: fewer items, a little dearer.
export const STORE_ITEMS = { water_500: 700, bars: 1800, ramen: 1100, batteries_aa: 5500, wipes: 2500, tissue: 1800, masks: 6000, candles: 4000 };

export const itemName = (id) => CATALOG[id]?.name ?? id;
