import Papa from "https://esm.sh/papaparse@5.5.3";

// Raw CSV strings for service offices and zipcode→coords
export const csvServiceText = `
"ID","ID2","Office","Address","TEL","FAX","Email","URL","業務内容","分類","タグ"
K01,ウキ,ウキウキファイブ,〒213-0032 神奈川県川崎市高津区久地4-13-3 タイヘイビル204号,044-812-3844,044-812-3845,kwsk1j@roukyou.gr.jp,,放課後教室ウキウキファイブ,Childcare Support,放課後教室
K03,川南,川崎南地域福祉事業所 みっぱ保育園,〒210-0814 神奈川県川崎市川崎区台町8-18 サンルミエール102,044-333-5515,044-328-7246,kawasaki-minami@roukyou.gr.jp,,川崎南事業所 みっぱ保育園,Childcare Support,保育園
K04,川市,川崎市ふれあい子育てサポート タック,〒211-0051 神奈川県川崎市中原区宮内2-15-15,044-740-3950,044-740-3970,ktfjtack@roukyou.gr.jp,http://ktfjtack.roukyou.gr.jp,川崎市ふれあい子育てサポート タック,Childcare Support,子育てサポート
K05,医協,医療生協かながわ保育室ぴーす,〒210-0833 神奈川県川崎市川崎区桜本2-1-22 協同こどもクリニック2F,044-280-4117,044-280-4119,takenoko@roukyou.gr.jp,,医療生協かながわ保育室ぴーす,Childcare Support,保育室
K06,平大,平塚市大野小学校区放課後児童クラブ,〒254-0906 神奈川県平塚市公所868,0463-50-5525,0463-50-5526,seibuhukusikaikan@me.scn-net.ne.jp,http://www.scn-net.ne.jp/~h_seibu/,平塚市大野小学校区放課後児童クラブ,Childcare Support,放課後児童クラブ
K07,平豊,平塚市豊田小学校区放課後児童クラブ,〒254-0084 神奈川県平塚市南豊田381,0463-68-6990,0463-68-6990,toyodagakudou@roukyou.gr.jp,http://toyodagakudou.roukyou.gr.jp,平塚市豊田小学校区放課後児童クラブ,Childcare Support,放課後児童クラブ
K08,平中,平塚市中原小学校区放課後児童クラブ,〒254-0061 神奈川県平塚市御殿2-8-9 中原小学校内,0463-33-7343,0463-33-7343,nakaharagakudou@roukyou.gr.jp,https://nakaharagakudou.roukyou.gr.jp/,平塚市中原小学校区放課後児童クラブ,Childcare Support,放課後児童クラブ
K09,森台,森の台キッズルーム,〒226-0029 神奈川県横浜市緑区森の台19-1,045-932-3095,045-932-3095,,http://m-kids.roukyou.gr.jp/,横浜みどり事業所（森の台キッズルーム）,Childcare Support,キッズルーム
K10,奈小,奈良小キッズクラブ,〒227-0036 神奈川県横浜市青葉区奈良町1541-2,045-962-7152,045-962-7152,,,横浜あおば事業所（奈良小キッズクラブ）,Childcare Support,放課後授業
S01,報徳,報徳ワーカーズ,〒258-0017 神奈川県足柄上郡大井町金手726-1,0465-83-1188,0465-20-9001,houtoku1188@gmail.com,,報徳ワーカーズ,Food & Agriculture,農業
C01,川事,川崎事業所,〒213-0032 神奈川県川崎市高津区久地4-13-3 タイヘイビル204号,044-812-3844,044-812-3845,kwsk1j@roukyou.gr.jp,,生田緑地／夢見ヶ先動植物公園／緑地等,Cleaning, Greenery & General Life Support,緑地;清掃
C02,川南,川崎南事業所,〒210-0814 神奈川県川崎市川崎区台町8-18 サンルミエール102,044-333-5515,044-328-7246,kawasaki-minami@roukyou.gr.jp,,川崎協同病院／大師診療所等,Cleaning, Greenery & General Life Support,清掃;医療
C03,横事,横浜事業所,〒245-0061 神奈川県横浜市戸塚区汲沢8-19-2 東明ハウス105,045-410-7604,045-410-7604,ykhmj@roukyou.gr.jp,,医療生協かながわ戸塚病院等,Cleaning, Greenery & General Life Support,清掃
C04,湘南,湘南事業所,〒251-0043 神奈川県藤沢市辻堂元町3-10-6 湘南サーフ2,0466-34-8533,0466-47-7006,synanj@roukyou.gr.jp,,多摩大学／相模生協病院等,Cleaning, Greenery & General Life Support,清掃;緑化
D01,放課,放課後等デイサービス オリーブ,〒252-0025 神奈川県座間市四ツ谷449,046-204-5577,046-204-5576,olive@roukyou.gr.jp,,障がい児ディサービス,Disability and Child Support,ディサービス
D02,児デ,児童デイサービスたんぽぽ,〒251-0875 神奈川県藤沢市本藤沢6-1-9 1F,0466-90-0516,0466-90-0517,fujisawa-tanpopo@roukyou.gr.jp,,児童デイサービスたんぽぽ,Disability and Child Support,児童デイサービス
D03,放等,放課後等デイサービスおひさま,〒252-0813 神奈川県藤沢市亀井野1514ファーストシティハウス1F,0466-77-5186,0466-77-6060,fujisawa-ohisama@roukyou.gr.jp,,放課後等デイサービスおひさま,Disability and Child Support,放課後デイサービス
D04,放キ,放課後等デイサービスキッズステーションゆう,〒252-0802 神奈川県藤沢市高倉650-56 コーポカネウン1F,0466-45-3024,0466-45-4563,fhsakrcg@roukyou.gr.jp,,放課後等デイサービスキッズステーションゆう,Disability and Child Support,放課後デイサービス
D05,生介,生活介護 六会ひだまり,〒252-0813 神奈川県藤沢市亀井野1511-1,046-654-9071,046-690-0517,,,"生活介護 六会ひだまり",Disability and Child Support,生活介護
P01,常台,常盤台コミュニティハウス,〒240-0067 神奈川県横浜市保土ヶ谷区常盤台53-2,045-348-8277,045-348-8288,tokiwdai-ch@space.ocn.ne.jp,http://tokiwadai-ch.roukyou.gr.jp,指定管理者,Public Works & Designated Management Services,指定管理者
P02,上白,上白根コミュニティハウス,〒241-0002 神奈川県横浜市旭区上白根233-6,045-954-1691,045-954-1692,info@y-kamisiranech,http://kamishirane-ch.roukyou.gr.jp/,指定管理者,Public Works & Designated Management Services,指定管理者
P03,横権,横浜市権太坂コミュニティハウス,〒240-0026 神奈川県横浜市保土ヶ谷区権太坂3-1-1 権太坂スクエアA棟1F,045-713-6625,045-713-6695,gontazaka_c@roukyou.gr.jp,http://gontazaka_c.roukyou.gr.jp,指定管理者,Public Works & Designated Management Services,指定管理者
P04,横霧,横浜市霧が丘コミュニティハウス,〒226-0016 神奈川県横浜市緑区霧が丘3-23,045-922-2100,045-922-0050,,,,指定管理者,Public Works & Designated Management Services,指定管理者
P05,相南,相模原南自立支援事業所 一休,〒252-0321 神奈川県相模原市南区相模台4-9-12,042-851-6591,042-851-5891,ikkyu@roukyou.gr.jp,,,,生活自立支援,Public Works & Designated Management Services,生活自立支援
P06,平西,平塚市西部福祉会館,〒254-0906 神奈川県平塚市公所868,0463-50-5525,0463-50-5526,,,,指定管理者,Public Works & Designated Management Services,指定管理者
P07,平南,平塚市南部福祉会館,〒254-0813 神奈川県平塚市袖ヶ浜20-1,0463-21-3370,0463-21-5355,,,,指定管理者,Public Works & Designated Management Services,指定管理者
P08,七国,七国荘,〒259-1205 神奈川県平塚市土屋4594,0463-58-1265,0463-58-1265,,,,指定管理者,Public Works & Designated Management Services,指定管理者
P09,三浦,三浦事業所,〒238-0224 神奈川県三浦市三崎町諸礒1870,046-882-6788,046-882-6782,miura@roukyou.gr.jp,http://miuraroufuku.roukyou.gr.jp/,指定管理者,Public Works & Designated Management Services,指定管理者
P10,権坂,権太坂コミュニティハウス,〒240-0026 神奈川県横浜市保土ヶ谷区権太坂3-1-1 権太坂スクエアA棟1F,045-713-6625,045-713-6695,gontazaka_c@roukyou.gr.jp,http://gontazaka_c.roukyou.gr.jp,指定管理者,Public Works & Designated Management Services,指定管理者
E01,TACK,TACK,〒211-0051 神奈川県川崎市中原区宮内2-15-15,044-740-3950,044-740-3970,ktfjtack@roukyou.gr.jp,,通所介護・訪問介護・居宅支援・介護予防,Elderly & Regional Welfare,介護
E02A,でデ,であいの家(デイサービス),〒244-0003 神奈川県横浜市戸塚区戸塚町2599,045-865-1279,045-865-1328,ykhttkfj@roukyou.gr.jp,,通所介護(パワーリハビリ)・訪問介護・介護予防、高齢者住宅管理及び生活支援,Elderly & Regional Welfare,高齢者福祉
E02B,で訪,であいの家(訪問介護),〒244-0003 神奈川県横浜市戸塚区戸塚町1545-2 戸塚の里 B101,045-864-1024,045-390-0980,deai-homon@roukyou.gr.jp,,デイサービス,Elderly & Regional Welfare,訪問介護
E03,ここ,ここち,〒245-0014 神奈川県横浜市泉区中田南5-37-18 ユーコーハイツ1F,045-803-5555,045-803-5556,kokochi@roukyou.gr.jp,,,,通所介護(パワーリハビリ)・介護予防・地域支援事業・配食,Elderly & Regional Welfare,地域支援事業
E04,かけ,かけはし,〒252-0802 神奈川県藤沢市高倉650-56 コーポカネウン1F,0466-45-3024,0466-45-4563,fhsakrcg@roukyou.gr.jp,,,,通所介護・介護予防,Elderly & Regional Welfare,介護
E05,まど,まどい,〒252-0813 神奈川県藤沢市亀井野1514ファーストシティハウス1F,0466-77-5186,0466-77-6060,fujisawa-ohisama@roukyou.gr.jp,,,,高齢者福祉,Elderly & Regional Welfare,福祉
E06,長後,長後あかり,〒252-0802 神奈川県藤沢市高倉650-56 コーポカネウン1F,0466-45-3024,0466-45-4563,fhsakrcg@roukyou.gr.jp,,,,訪問介護・通所介護・障害者総合支援・子育て短期支援(訪問型)・トワイライトステイ(夜の子ども一時預かり)・配食サービス,Elderly & Regional Welfare,訪問介護
E07,藤沢,藤沢あかり,〒252-0813 神奈川県藤沢市亀井野1514ファーストシティハウス1F,0466-77-5186,0466-77-6060,fhsakrcg@roukyou.gr.jp,,,,訪問介護・通所介護・居宅支援・介護予防・藤沢市1人親世帯日常生活支援事業,Elderly & Regional Welfare,介護予防
E08,かが,かがやき,〒245-0014 神奈川県横浜市泉区中田南5-37-18 ユーコーハイツ1F,045-803-5555,045-803-5556,kokochi@roukyou.gr.jp,,,,通所介護・講座事業,Elderly & Regional Welfare,講座事業
E09,もみ,もみじ,〒252-0813 神奈川県藤沢市亀井野1514ファーストシティハウス1F,0466-77-5186,0466-77-6060,fujisawa-ohisama@roukyou.gr.jp,,,,高齢者福祉,Elderly & Regional Welfare,高齢者福祉
E10,yell,yell,〒252-0802 神奈川県藤沢市高倉650-30,0466-47-6676,0466-47-6676,kurasapo-c@roukyou.gr.jp,http://yell.roukyou.gr.jp/,生活支援コーディネーター事業・藤沢市「地域の縁側」(基幹型)・コミュニティカフェ、講座・イベント等定期プログラム,Elderly & Regional Welfare,生活支援コーディネーター
E11,明日,明日香辻堂,〒252-0802 神奈川県藤沢市高倉650-30 コーポ渡邊2F,0466-47-6676,0466-47-6676,kurasapo-c@roukyou.gr.jp,,,,高齢者福祉,Elderly & Regional Welfare,福祉
E12,戸塚,戸塚の里,〒252-0802 神奈川県藤沢市高倉650-30 コーポ渡邊2F,0466-47-6676,0466-47-6676,kurasapo-c@roukyou.gr.jp,,,,高齢者介護,Elderly & Regional Welfare,介護
H01,事本,ワーカーズコープ 神奈川事業本部,〒231-0045 神奈川県横浜市中区伊勢佐木町2丁目66番 満利屋ビル8F,045-341-4192,045-260-5558,kngwb@roukyou.gr.jp,,ワーカーズコープ 神奈川事業本部,Regional HQ,
`;

export const csvCoordText = `
Zipcode,Latitude,Longitude
210-0814,35.5303,139.7327
210-0833,35.5210,139.7238
211-0051,35.5902,139.6421
213-0032,35.6122,139.6022
226-0016,35.5047,139.5481
226-0029,35.5093,139.5330
227-0036,35.5621,139.4828
230-0001,35.5335,139.6796
231-0026,35.4438,139.6456
238-0224,35.1488,139.6305
240-0026,35.4436,139.5932
240-0067,35.4734,139.5909
243-0402,35.4632,139.4120
244-0003,35.3911,139.5273
245-0014,35.4063,139.5096
245-0061,35.3991,139.5146
245-0062,35.3911,139.5178
251-0035,35.3134,139.4866
251-0043,35.3308,139.4467
251-0047,35.3308,139.4467
251-0875,35.3554,139.4706
252-0025,35.4739,139.3861
252-0303,35.5315,139.4345
252-0321,35.5172,139.4107
252-0802,35.4128,139.4714
252-0813,35.3784,139.4751
254-0014,35.3601,139.3676
254-0018,35.3578,139.3539
254-0061,35.3451,139.3292
254-0084,35.3563,139.3293
254-0813,35.3176,139.3506
254-0906,35.3464,139.3016
259-1205,35.3429,139.2550
258-0017,35.3194,139.1504
241-0002,35.4925,139.5395
231-0045,35.4465,139.6325
`;

// Function to process the CSV and return the data with region information
export function getProcessedOfficeData() {
  const { data: services } = Papa.parse(csvServiceText.trim(), {
    header: true, skipEmptyLines: "greedy"
  });

  const { data: coords } = Papa.parse(csvCoordText.trim(), {
    header: true, skipEmptyLines: "greedy"
  });

  // Build zipcode → {lat, lon} map
  const coordMap = {};
  coords.forEach(c => {
    coordMap[c.Zipcode] = {
      lat: +c.Latitude,
      lon: +c.Longitude
    };
  });

  // Function to determine region number based on Address column using regex
  function getRegionByAddress(address) {
    const regionPatterns = [
      { region: 1, pattern: /川崎/ },
      { region: 2, pattern: /横浜/ },
      { region: 3, pattern: /湘南|三浦|藤沢|平塚/ },
      { region: 4, pattern: /海老名|相模原|座間/ },
      { region: 5, pattern: /小田原|足柄/ },
    ];

    for (let i = 0; i < regionPatterns.length; i++) {
      if (regionPatterns[i].pattern.test(address)) {
        return regionPatterns[i].region;
      }
    }
    return 2; // default to Yokohama
  }

  // Function to map region number to region name
  function getRegionNameByNumber(regionNumber) {
    const regionNames = {
      1: "川崎",       
      2: "横浜",       
      3: "湘南・三浦", 
      4: "県央",       
      5: "県西",       
    };
    return regionNames[regionNumber] || "Unknown Region";
  }

  // Merge the service and coordinate data
  return services.map(d => {
    const m = d.Address.match(/〒\s*([\d\-]+)/);
    const zip = m ? m[1] : null;
    const { lat = 0, lon = 0 } = coordMap[zip] || {};
    const regionNumber = getRegionByAddress(d.Address);
    const regionName = getRegionNameByNumber(regionNumber);

    return {
      id: d.ID,
      id2: d.ID2,
      office: d.Office,
      address: d.Address,
      tel: d.TEL,
      fax: d.FAX,
      email: d.Email,
      url: d.URL,
      content: d['業務内容'],
      category: d['分類'],
      tags: d['タグ'],
      lat,
      lon,
      regionNumber,
      regionName
    };
  });
}
