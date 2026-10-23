// Ports game/facts.js: verified sources and facts. Every safety line in the game comes from here (item 9).
// Every line was checked against the opened source page on the date in checkedAt
// (research + adversarial re-check, 2026-10-19/20). The game states safety advice only from here.
// Where official pages disagree (candles), both sides are kept and the game says so.
// The tables below are generated from the JS module (Korean text verbatim, insertion order kept).
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    public sealed class SourceEntry
    {
        public string id, publisher, title, url, checkedAt;
    }

    public sealed class FactEntry
    {
        public string id, topic, sourceId, text;
    }

    public sealed class CiteResult
    {
        public string text, source, url;
    }

    public static class Facts
    {
        // JS: SOURCES.map(s => ({ ...s, checkedAt: '2026-10-20' })).
        static SourceEntry Src(string id, string publisher, string title, string url) => new SourceEntry { id = id, publisher = publisher, title = title, url = url, checkedAt = "2026-10-20" };
        static FactEntry Fct(string id, string topic, string sourceId, string text) => new FactEntry { id = id, topic = topic, sourceId = sourceId, text = text };

        public static readonly List<SourceEntry> SOURCES = new List<SourceEntry>
        {
            Src("ps_emergency", "국민안전24", "비상사태 행동요령", "https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?menuSn=4&category=stateOfEmergency&actsHeaderTitle=%EB%B9%84%EC%83%81%EC%82%AC%ED%83%9C"),
            Src("ps_prepare", "국민안전24", "비상대비 행동요령", "https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?menuSn=4&category=stateOfEmergency&actsHeaderTitle=%EB%B9%84%EC%83%81%EB%8C%80%EB%B9%84"),
            Src("ps_airraid", "국민안전24", "민방공 경보 행동요령", "https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?menuSn=4&category=civlAirDefenseWarning&actsHeaderTitle=%EB%AF%BC%EB%B0%A9%EA%B3%B5%EA%B2%BD%EB%B3%B4"),
            Src("ps_er4", "국민안전24", "비상사태 시 국민행동요령", "https://safekorea.go.kr/safekorea-kor/em/emergency/emergency_er4.html"),
            Src("ps_df1", "국민안전24", "민방공 경보 발령 시 행동요령", "https://www.safekorea.go.kr/safekorea-kor/em/emergency/emergency_df1.html"),
            Src("ps_power", "국민안전24", "정전·전력부족 행동요령", "https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?menuSn=4&category=powerOutagesandShortages"),
            Src("ps_typhoon", "국민안전24", "태풍 행동요령", "https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?menuSn=4&category=typhoon&actsHeaderTitle=%ED%83%9C%ED%92%8D"),
            Src("ps_fire", "국민안전24", "화재 행동요령", "https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?menuSn=4&category=fires&actsHeaderTitle=%ED%99%94%EC%9E%AC"),
            Src("ps_comms", "국민안전24", "방송통신재난 행동요령", "https://safekorea.go.kr/safekorea-kor/acts/nacts/action-guide.do?menuSn=4&category=infocommAccident"),
            Src("mois_extinguisher", "행정안전부 어린이 안전배움터", "소화기 사용법", "https://www.mois.go.kr/chd/sub/a06/fire_2/screen.do"),
            Src("mois_civildefense", "행정안전부", "민방위 업무 안내(비상급수시설)", "https://www.mois.go.kr/frt/sub/a06/b12/civildefensework/screen.do"),
            Src("mois_four", "행정안전부", "비상시 꼭 기억할 4가지", "https://www.mois.go.kr/frt/bbs/type002/commonSelectBoardArticle.do?bbsId=BBSMSTR_000000000215&nttId=110500"),
            Src("law_alert_signal", "국가법령정보센터", "민방위 경보 신호 및 전달 방법(별표)", "https://www.law.go.kr/LSW/flDownload.do?bylClsCd=200201&flSeq=160780623"),
            Src("law_alert_action", "국가법령정보센터", "민방위 경보별 국민 행동요령(별표)", "https://www.law.go.kr/LSW/flDownload.do?bylClsCd=200201&flSeq=160780663"),
            Src("law_alert_text", "국가법령정보센터", "민방위 경보 문자 표준 문안(별표)", "https://www.law.go.kr/LSW/flDownload.do?bylClsCd=200201&flSeq=160780739"),
            Src("law_broadcast", "국가법령정보센터", "방송통신발전 기본법 제40조·제40조의2", "https://www.law.go.kr/%EB%B2%95%EB%A0%B9/%EB%B0%A9%EC%86%A1%ED%86%B5%EC%8B%A0%EB%B0%9C%EC%A0%84%EA%B8%B0%EB%B3%B8%EB%B2%95"),
            Src("law_disaster", "국가법령정보센터", "재난 및 안전관리 기본법 제40조(대피명령)", "https://www.law.go.kr/%EB%B2%95%EB%A0%B9/%EC%9E%AC%EB%82%9C%EB%B0%8F%EC%95%88%EC%A0%84%EA%B4%80%EB%A6%AC%EA%B8%B0%EB%B3%B8%EB%B2%95"),
            Src("law_sign", "국가법령정보센터", "비상대피시설 안내·유도표지판(별표 1)", "https://www.law.go.kr/LSW/flDownload.do?gubun=&flSeq=159674105&bylClsCd=110201"),
            Src("kr_shelters", "대한민국 정책브리핑", "민방위 훈련과 대피소 찾기(2026)", "https://www.korea.kr/news/policyNewsView.do?newsId=148970287"),
            Src("kr_siren", "대한민국 정책브리핑", "민방위 경보 체계 개편(2023)", "https://www.korea.kr/news/policyNewsView.do?newsId=148919077"),
            Src("kr_access", "대한민국 정책브리핑", "안전취약계층 대피소 개선", "https://www.korea.kr/briefing/actuallyView.do?newsId=148921302"),
            Src("kr_fire_apt", "대한민국 정책브리핑", "아파트 화재, 상황을 판단해 대피", "https://www.korea.kr/news/policyNewsView.do?newsId=148922477"),
            Src("kr_fire_apt2", "대한민국 정책브리핑", "아파트 화재 피난 행동요령", "https://www.korea.kr/news/policyNewsView.do?newsId=148941490"),
            Src("kr_candle", "대한민국 정책브리핑(소방청)", "촛불 화재 예방", "https://www.korea.kr/multi/visualNewsView.do?newsId=148907183"),
            Src("kr_fridge", "대한민국 정책브리핑(식약처)", "정전 시 식품 안전", "https://www.korea.kr/news/policyNewsView.do?newsId=148904584"),
            Src("kr_psych", "대한민국 정책브리핑", "재난심리회복지원센터 1670-9512", "https://www.korea.kr/news/policyNewsView.do?newsId=148879989"),
            Src("nfa_plan", "소방청", "우리 아파트 대피계획 세우기", "https://www.nfa.go.kr/nfa/news/pressrelease/press/?boardId=bbs_0000000000000010&mode=view&cntId=2067"),
            Src("seoul_shelter", "서울특별시 안전누리", "비상시 대피 요령", "https://news.seoul.go.kr/safe/archives/2094"),
            Src("gangdong_bag", "서울 강동구", "생존배낭 꾸리기", "https://gangdong.go.kr/web/newportal/contents/gdp_005_006_004_001_002"),
            Src("incheon_supplies", "인천광역시", "비상대비 물자", "https://www.incheon.go.kr/safe/SAFE020606"),
            Src("incheon_terror", "인천광역시", "테러 대비 행동요령", "https://www.incheon.go.kr/safe/SAFE020600"),
            Src("yeonsu_water", "인천 연수구", "민방위 비상급수시설", "https://www.yeonsu.go.kr/safety/headquarters/defense/facility.asp"),
            Src("kwater", "K-water", "식용수 사고 행동요령", "https://www.kwater.or.kr/safety/sub03/soci01Page.do?s_mid=1942"),
            Src("kdca_hands", "질병관리청 국가건강정보포털", "손씻기", "https://health.kdca.go.kr/healthinfo/biz/health/gnrlzHealthInfo/gnrlzHealthInfo/gnrlzHealthInfoView.do?cntnts_sn=6545"),
            Src("nct", "국가트라우마센터", "재난 후 심리지원", "https://nct.go.kr/distMental/crisis/crisis01_2_13.do"),
            Src("sphere", "Sphere (Oxford prototype)", "급수 접근성과 물의 양", "https://sphereprototype.conted.ox.ac.uk/standards/watersupply/accessAndWaterQuantity.html"),
            Src("ready_kit", "Ready.gov (미국, 보조 자료)", "Build a Kit", "https://www.ready.gov/kit"),
            Src("ready_tech", "Ready.gov (미국, 보조 자료)", "Get Tech Ready", "https://www.ready.gov/get-tech-ready"),
            Src("ready_power", "Ready.gov 한국어판 (미국, 보조 자료)", "정전", "https://www.ready.gov/ko/power-outages"),
            Src("ready_evac", "Ready.gov (미국, 보조 자료)", "Evacuation", "https://www.ready.gov/evacuation"),
            Src("cdc_food", "CDC (미국, 보조 자료)", "Keep Food Safe After an Emergency", "https://www.cdc.gov/food-safety/foods/keep-food-safe-after-emergency.html"),
            Src("cdc_chem", "CDC (미국, 보조 자료)", "Shelter in place (chemical)", "https://www.cdc.gov/chemical-emergencies/response/shelter-in-place.html"),
            Src("cdc_rad", "CDC (미국, 보조 자료)", "Radiation emergencies: Get inside, stay inside, stay tuned", "https://www.cdc.gov/radiation-emergencies/response/index.html"),
            Src("cdc_sarin", "CDC (미국, 보조 자료)", "Sarin fact sheet", "https://www.cdc.gov/chemical-emergencies/chemical-fact-sheets/sarin.html"),
            Src("cdc_paraquat", "CDC (미국, 보조 자료)", "Paraquat fact sheet", "https://www.cdc.gov/chemical-emergencies/chemical-fact-sheets/paraquat.html"),
            Src("dhs_signs", "미국 국토안보부 (보조 자료)", "Recognize the Signs", "https://www.dhs.gov/see-something-say-something/recognize-the-signs"),
            Src("icrc_info", "ICRC", "전시 유해 정보 Q&A", "https://www.icrc.org/en/article/harmful-information-questions-and-answers"),
            Src("koddi_card", "한국장애인개발원", "재난 대비 카드", "https://www.koddi.or.kr/webzine/didimdol/vol304/page01-03.html"),
            Src("etiquette", "금천구", "장애인 에티켓", "https://www.geumcheon.go.kr/portal/contents.do?key=787"),
            Src("unhcr_disability", "UNHCR", "장애인 보호 지침", "https://emergency.unhcr.org/protection/persons-risk/persons-disabilities"),
            Src("press_code", "한국기자협회", "재난보도준칙", "https://www.journalist.or.kr/news/section4.html?p_num=10"),
            Src("sms_list", "국민안전24", "재난문자 목록", "https://safekorea.go.kr/safekorea-kor/ctim/cmsg/calamitySms.do?menuSn=34&firstYn=Y"),
            Src("sms_area", "행정안전부", "재난문자방송 기준 및 운영규정", "https://www.law.go.kr/LSW/admRulInfoP.do?admRulSeq=2100000274384&chrClsCd=010201"),
            Src("seoul_2023", "서울신문", "2023년 5월 31일 경계경보 문자", "https://m.seoul.co.kr/news/2023/06/01/20230601002003"),
        };

        // topic: bag | home | contacts | power | comms | alert | shelter | fire | water | food | evac | info | people | mind | myth
        public static readonly List<FactEntry> FACTS = new List<FactEntry>
        {
            // 생존배낭과 집 비축
            Fct("bag_days", "bag", "ps_emergency", "생존배낭에는 평소 2~3일분의 식수, 식량, 생필품, 상비약, 라디오, 손전등 등을 담아 둡니다."),
            Fct("bag_each", "bag", "ps_emergency", "생존배낭은 각자 들고 이동할 수 있도록 한 사람 분량씩 준비합니다."),
            Fct("bag_nocook", "bag", "ps_emergency", "배낭 속 음식은 되도록 조리 없이 먹을 수 있는 것으로 고르고, 유통기한을 정기적으로 확인해 바꿉니다."),
            Fct("bag_door", "bag", "gangdong_bag", "생존배낭은 72시간(3일)을 기준으로, 현관처럼 들고 나가기 쉬운 곳에 둡니다."),
            Fct("bag_copies", "bag", "gangdong_bag", "신분증·통장·임대차계약서 같은 중요서류는 복사본을, 가족·지인 연락처는 적어서 배낭에 넣습니다."),
            Fct("bag_cash", "bag", "gangdong_bag", "생존배낭 목록에는 현금과 신용카드가 함께 들어 있습니다."),
            Fct("bag_foil", "bag", "gangdong_bag", "침낭 대신 은박담요를 쓰면 부피와 무게를 줄일 수 있습니다."),
            Fct("bag_purpose", "bag", "ps_emergency", "생존배낭은 대피소 등에 대피한 뒤 생활하는 데 필요한 최소한의 생활용품입니다."),
            Fct("bag_take", "bag", "ps_emergency", "공습경보가 울리면 생존배낭을 가지고 대피시설이나 지하 장소로 빨리 이동합니다."),
            Fct("home_stock", "home", "ps_emergency", "집에는 생활필수품과 상비약을 갖춰 두고, 생존배낭과 겹치는 물품은 배낭에 따로 담아 둡니다."),
            Fct("home_30", "home", "incheon_supplies", "인천시 안내는 집의 식량·식수를 쌀·라면·통조림처럼 조리와 보관이 쉬운 것으로 30일분 갖추라고 합니다."),
            Fct("home_tub", "home", "ps_emergency", "단전·단수에 대비해 손전등(건전지)을 준비하고, 욕조나 큰 그릇에 물을 받아 두고 아껴 씁니다."),
            Fct("home_stove", "home", "ps_emergency", "지정 대피소가 아닌 곳에 머물 때는 가스 공급 중단에 대비해 휴대용 가스레인지와 부탄캔을 준비합니다."),
            Fct("home_nohoard", "home", "ps_er4", "비상사태 때는 생활필수품을 사재기하지 말고 정부의 배급에 협조합니다."),
            Fct("home_clothes", "home", "ps_emergency", "한 사람당 최소 한 벌씩 추위를 막을 따뜻한 옷과 신발을 준비합니다."),
            // 연락처와 서류
            Fct("contact_card", "contacts", "ps_emergency", "통신이 끊길 수 있으니 연락처를 미리 적어 두고, 비상연락카드를 써서 생존배낭에 넣어 둡니다."),
            Fct("contact_fields", "contacts", "ps_emergency", "비상연락카드에는 개인사항, 가족사항, 혈액형, 복용약, 집·직장 연락처, 신고·구호기관 연락처를 적습니다."),
            Fct("contact_meet", "contacts", "ps_emergency", "평소 약속한 곳이나 안전한 대피소를 1차, 2차, 3차 만남의 장소로 정해 둡니다."),
            Fct("docs_take", "contacts", "ps_emergency", "대피할 때는 신분증 같은 중요서류도 함께 챙깁니다."),
            // 정전
            Fct("power_prep", "power", "ps_power", "갑작스러운 정전에 대비해 손전등, 비상 식음료, 휴대용 라디오를 준비해 둡니다."),
            Fct("power_home", "power", "ps_power", "우리 집만 정전이면 플러그를 뽑고 스위치를 끈 뒤 분전반의 누전차단기를 확인합니다."),
            Fct("power_lift", "power", "ps_power", "정전으로 엘리베이터에 갇히면 인터폰으로 구조를 요청하고 스스로 빠져나오려 하지 않습니다."),
            Fct("power_123", "power", "ps_power", "정전 신고와 전기 상담은 국번 없이 123, 구조 요청은 119입니다."),
            Fct("power_plugs", "power", "ps_power", "전기가 다시 들어오면 가전제품 플러그를 하나씩 순서대로 꽂습니다."),
            Fct("candle_no", "power", "ps_typhoon", "국민안전24 태풍 행동요령: 정전 때 양초를 쓰지 말고 손전등이나 휴대폰을 사용합니다."),
            Fct("candle_prep", "power", "ps_power", "국민안전24 정전 행동요령: 양초, 랜턴 등 정전 시 비상조명기구를 준비합니다. (공식 자료끼리 양초 안내가 다릅니다.)"),
            Fct("candle_rules", "power", "kr_candle", "촛불 화재는 대부분 켜 둔 채 자거나 자리를 비웠을 때 납니다. 자기 전·외출 전에 끄고, 불연 받침대에 고정합니다."),
            Fct("night_lights", "power", "ps_prepare", "경계경보가 내리면 밤에는 불을 끄고 커튼으로 불빛이 밖으로 새지 않게 하며, 전열기 코드를 뽑습니다."),
            // 통신과 방송
            Fct("radio_batt", "comms", "ps_emergency", "정부 안내 방송을 듣기 위해 라디오 건전지는 여분을 충분히 준비해 둡니다."),
            Fct("radio_listen", "comms", "ps_er4", "비상사태에는 TV·라디오의 민방위 방송을 계속 들으며 정부 안내에 따릅니다."),
            Fct("radio_kbs", "comms", "law_broadcast", "법에 따라 한국방송공사(KBS)가 재난방송 등의 주관방송사로 지정되어 있습니다."),
            Fct("comms_calls", "comms", "ps_emergency", "통화량이 갑자기 늘면 통신망이 마비될 수 있으니 불필요한 전화는 삼갑니다."),
            Fct("comms_text", "comms", "ready_tech", "통신망이 붐빌 때는 통화보다 문자 메시지가 대역폭을 적게 써서 더 잘 전달됩니다. (미국 Ready.gov)"),
            Fct("comms_battery", "comms", "ready_tech", "정전 때는 기기 사용을 줄이고 저전력 모드로 배터리를 아낍니다. (미국 Ready.gov)"),
            Fct("comms_wifi", "comms", "ps_comms", "통신재난 때 재난 와이파이가 열리면 Wi-Fi 설정에서 Public Wifi Emergency를 고릅니다."),
            Fct("comms_112", "comms", "ps_comms", "통신이 안 되더라도 119, 112 같은 긴급전화는 연결할 수 있습니다."),
            // 경보
            Fct("alert_airraid", "alert", "law_alert_action", "공습경보가 울리면 즉시 가까운 지하시설이나 유리창이 없는 건물 안으로 대피하고 TV·라디오·문자 안내에 따릅니다."),
            Fct("alert_siren", "alert", "law_alert_signal", "공습경보 사이렌은 1분간 울리는 파상음입니다(5초 상승·3초 하강 반복)."),
            Fct("alert_sms", "alert", "law_alert_text", "경보 문자는 시각, 원인, 지역, 경보 종류, 행동 순서로 짧게 쓰입니다."),
            Fct("alert_stairs", "alert", "ps_emergency", "고층 건물에 있다면 엘리베이터를 쓰지 말고 계단으로 지하실이나 아래층으로 내려갑니다."),
            Fct("alert_night", "alert", "law_alert_action", "야간에 공습경보가 울리면 실내외 전등을 모두 끈 다음 대피합니다."),
            Fct("alert_caution", "alert", "ps_airraid", "경계경보 때는 즉시 대피할 준비를 하고, 어린이와 노약자는 미리 대피시킵니다. 유류·가스는 안전한 곳으로 옮기고 전열기 코드를 뽑습니다."),
            Fct("alert_downgrade", "alert", "law_alert_action", "공습경보가 경계경보로 바뀌어도 위험이 끝난 것이 아닙니다. 경보해제가 되어야 일상으로 돌아갑니다."),
            Fct("alert_drill", "alert", "kr_shelters", "민방위 훈련은 공습경보 → 15분 뒤 경계경보(경계태세로 통행 가능) → 경보해제 순서로 진행됩니다."),
            // 대피소
            Fct("shelter_find", "shelter", "kr_shelters", "민방위 대피소는 전국 1만 7천여 곳이며, 지도앱과 안전디딤돌 앱에서 찾을 수 있습니다."),
            Fct("shelter_near", "shelter", "seoul_shelter", "비상시에는 우선 가장 가까운 지하시설로 긴급대피한 뒤, 경보와 민방위대원의 안내에 따라 지정 대피시설로 이동합니다."),
            Fct("shelter_know", "shelter", "mois_four", "비상시 꼭 기억할 첫 번째는 대피장소를 미리 파악하고 생존배낭을 준비하는 것입니다."),
            Fct("shelter_signs", "shelter", "law_sign", "대피소 유도표지판은 흰 바탕에 파란 달리는 사람과 거리(10~50m), 방향 화살표로 대피소를 가리킵니다."),
            Fct("shelter_rules", "shelter", "ps_emergency", "대피소에서는 관리자의 안내를 따르고, 출입구와 통로를 막지 않으며, 불을 써서 요리하지 않습니다."),
            Fct("shelter_rumor", "shelter", "ps_emergency", "대피소에서는 정부 안내방송에 귀 기울이고 SNS로 퍼지는 유언비어에 주의합니다."),
            Fct("shelter_share", "shelter", "ps_emergency", "스스로 대처하기 어려운 이웃에게 정보를 나누고, 화장실 등 공용 시설은 깨끗하게 아껴 씁니다."),
            Fct("shelter_access", "shelter", "kr_access", "행정안전부는 유도블록과 출입구 경사로를 갖춘 시설을 대피소로 우선 지정하겠다고 밝혔습니다."),
            // 화재
            Fct("fire_shout", "fire", "ps_fire", "불을 발견하면 \"불이야!\" 하고 크게 외치거나 비상벨을 눌러 주변에 알립니다."),
            Fct("fire_small", "fire", "ps_fire", "불길이 천장까지 닿지 않은 작은 불이면 소화기로 신속히 끕니다."),
            Fct("fire_big", "fire", "ps_fire", "불길이 커지면 끄려 하지 말고, 젖은 수건 등으로 코와 입을 막고 몸을 낮춰 계단으로 대피합니다."),
            Fct("fire_119", "fire", "ps_fire", "119 신고는 하되, 신고하느라 대피 시간을 놓치지 않습니다."),
            Fct("fire_lift", "fire", "ps_fire", "화재로 대피할 때는 엘리베이터를 이용하지 않고 계단으로 지상까지 내려갑니다."),
            Fct("fire_door", "fire", "ps_fire", "손등으로 문손잡이를 만져 뜨거우면 반대편에 불이 난 것이니 문을 열지 않습니다."),
            Fct("fire_judge", "fire", "kr_fire_apt", "아파트 화재는 무조건 대피하기보다 상황을 판단해 대피하는 것이 중요합니다. 인명피해의 39%가 대피 중에 났습니다."),
            Fct("fire_stay", "fire", "ps_fire", "다른 세대의 불로 우리 집에 화염·연기가 들어오지 않으면 집 안에서 창문을 닫고 대기하며 119에 신고합니다."),
            Fct("ext_1", "fire", "mois_extinguisher", "소화기를 불이 난 곳으로 옮기고, 손잡이의 안전핀을 뽑습니다."),
            Fct("ext_2", "fire", "mois_extinguisher", "바람을 등지고 서서 호스(노즐)를 불 쪽으로 향합니다."),
            Fct("ext_3", "fire", "mois_extinguisher", "손잡이를 힘껏 움켜쥐고 빗자루로 쓸듯이 뿌립니다."),
            Fct("ext_door", "fire", "ps_fire", "실내에서는 밖으로 대피할 수 있도록 문을 등지고 소화기를 씁니다."),
            Fct("fire_candle", "fire", "kr_candle", "촛불 화재에 물을 뿌리면 파라핀 때문에 더 커질 수 있습니다. 소화기를 쓰거나 담요를 덮어 산소를 막습니다."),
            // 물과 음식
            Fct("water_point", "water", "mois_civildefense", "민방위 비상급수시설은 상수도가 끊겼을 때 복구 전까지 1인 1일 25리터를 임시로 공급하는 시설입니다."),
            Fct("water_sphere", "water", "sphere", "국제 인도주의 기준(Sphere)으로 사람이 마실 물은 하루 2.5~3리터, 마시고 씻고 조리하는 물은 최소 15리터입니다."),
            Fct("water_cut", "water", "kwater", "급수가 중단되면 관계 기관의 안내가 있기 전까지 그 물을 마시거나 양치·설거지에 쓰지 않습니다."),
            Fct("water_hands", "water", "kdca_hands", "물을 쓸 수 없을 때는 알코올 60% 이상 손소독제로 손을 소독합니다."),
            Fct("food_fridge", "food", "ready_power", "정전 중 문을 닫아 두면 냉장고는 약 4시간, 가득 찬 냉동고는 약 48시간 온도를 유지합니다. (미국 FEMA)"),
            Fct("food_4h", "food", "cdc_food", "전기 없이 4시간이 지나면 냉장고 속 고기, 생선, 남은 음식처럼 상하기 쉬운 음식은 버립니다. (미국 CDC)"),
            Fct("food_door", "food", "kr_fridge", "정전되면 냉장·냉동 상태를 지키도록 냉장고 문을 자주 열지 않습니다. (식약처)"),
            Fct("food_doubt", "food", "ready_power", "냄새·색·질감이 이상한 음식은 버립니다. 의심스러우면 버립니다."),
            // 대피와 정보
            Fct("evac_order", "evac", "law_disaster", "시장·군수·구청장 등이 대피명령을 내리면 즉시 따라야 합니다."),
            Fct("evac_note", "evac", "ready_evac", "대피할 때는 언제 어디로 떠나는지 쪽지를 남기고, 권고된 경로를 따릅니다. 지름길은 막혀 있을 수 있습니다. (미국 Ready.gov)"),
            Fct("evac_kit", "evac", "ready_evac", "대피할 때는 미리 꾸려 둔 비상용품 가방을 가져갑니다. (미국 Ready.gov)"),
            Fct("evac_wires", "evac", "ready_evac", "끊어진 전선에는 치명적인 전압이 흐를 수 있으니 피합니다. (미국 Ready.gov)"),
            Fct("info_rumor", "info", "ps_er4", "비상시에는 거짓 선전이나 유언비어에 흔들리지 말고 공식 안내를 확인합니다."),
            Fct("info_harm", "info", "icrc_info", "ICRC는 잘못된 경보나 구호 정보가 민간인을 위험에 노출시킬 수 있다고 설명합니다."),
            Fct("info_misfire", "info", "seoul_2023", "2023년 5월 31일 서울시 경계경보 문자에는 발령 이유와 대피 장소가 없었고, 이후 오발령 논란이 있었습니다."),
            Fct("info_suspect", "info", "incheon_terror", "폭발물로 의심되는 물건은 절대 손대지 말고 빨리 떨어진 뒤 경찰에 신고합니다."),
            Fct("info_appearance", "info", "dhs_signs", "인종, 민족, 성별, 출신 국가, 종교, 장애는 그 자체로 수상한 것이 아닙니다. 판단 근거는 행동입니다. (미국 국토안보부)"),
            Fct("info_scope", "info", "press_code", "재난보도준칙은 전체 상황을 모를 때 단편적인 정보를 전한다면, 부족하거나 더 확인할 부분을 함께 밝히도록 정합니다."),
            Fct("info_area", "info", "sms_area", "재난문자는 영향 범위에 따라 읍·면·동 단위까지 지역을 좁혀 보냅니다. 내가 받은 안내는 내가 있던 곳 기준입니다."),
            // 사람
            Fct("people_order", "people", "mois_four", "비상시 꼭 기억할 네 번째는 질서 유지와 상호 배려입니다."),
            Fct("people_ask", "people", "etiquette", "장애인을 도울 때는 그 사람이 무엇을 원하는지 잘 듣고, 요청이 있을 때 돕습니다. 혼자 판단해서 하는 행동은 친절이 아닙니다."),
            Fct("people_card", "people", "koddi_card", "재난 전에 장애 유형, 의료 정보, 의사소통 방법을 담은 재난 대비 카드를 써 두면 도움이 됩니다."),
            Fct("people_unhcr", "people", "unhcr_disability", "장애인과 가족에게 직접 물어 필요한 것과 할 수 있는 것을 파악하고, 이동할 때 보조기기·약과 떼어 놓지 않습니다. (UNHCR)"),
            // 마음
            Fct("mind_call", "mind", "kr_psych", "재난을 겪은 사람은 재난심리회복지원센터(1670-9512)에서 심리상담을 받을 수 있습니다."),
            Fct("mind_breath", "mind", "nct", "재난 직후에는 간단한 호흡법이 도움이 되며, 곁에 있는 사람의 말을 판단하지 않고 들어 줍니다."),
            // 이번 판에서 다루지 않는 것 (기획 검토 9항)
            Fct("myth_posture", "myth", "ps_er4", "핵폭발 시 엎드려 눈·귀를 막고 입을 벌리는 자세는 국민안전24에 \"대피할 시간이 없을 때\"라는 조건과 함께 있습니다. 이 게임은 핵·화생방 상황을 다루지 않습니다."),
            Fct("myth_high", "myth", "cdc_sarin", "화학 위협에서 \"무조건 높은 곳\"은 정답이 아닙니다. 실내에 사린이 퍼졌다면 건물 밖으로 나가라는 안내도 있습니다. (미국 CDC)"),
            Fct("myth_charcoal", "myth", "cdc_paraquat", "활성탄은 특정 독성물질을 삼켰을 때의 의료 처치로 쓰일 뿐, 일반적인 화학물질 제독 수단으로 확인되지 않았습니다. (미국 CDC)"),
            Fct("myth_meat", "myth", "cdc_food", "익힌 고기도 냉장 없이 4시간이 지나면 버려야 합니다. \"익히면 오래 간다\"는 근거가 없습니다. (미국 CDC)"),
            Fct("myth_underground", "myth", "ps_emergency", "지하시설이 언제나 안전한 것은 아닙니다. 장마·태풍 때는 침수 위험이 있는 저지대 지하시설로 대피하지 않습니다."),
        };

        static readonly Dictionary<string, FactEntry> byId = Index(FACTS, f => f.id);
        static readonly Dictionary<string, SourceEntry> srcById = Index(SOURCES, s => s.id);

        public static FactEntry Fact(string id) => id != null && byId.TryGetValue(id, out var f) ? f : null;
        public static SourceEntry Source(string id) => id != null && srcById.TryGetValue(id, out var s) ? s : null;

        // "text — publisher · title" for cards and captions: { text, source, url } or null.
        public static CiteResult Cite(string id)
        {
            var f = Fact(id);
            if (f == null) return null;
            var s = Source(f.sourceId);
            return new CiteResult { text = f.text, source = s != null ? s.publisher + " · " + s.title : "", url = s?.url ?? "" };
        }

        // new Map(list.map(x => [x.id, x])): a later duplicate id wins, as in JS.
        static Dictionary<string, T> Index<T>(List<T> list, Func<T, string> key)
        {
            var d = new Dictionary<string, T>();
            foreach (var x in list) d[key(x)] = x;
            return d;
        }
    }
}
