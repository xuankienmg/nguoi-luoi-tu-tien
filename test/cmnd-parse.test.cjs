const test = require('node:test'), assert = require('node:assert');
const { parse, merge, cardBox } = require('../site/js/cmnd-parse.js');

test('CCCD gắn chip: nhãn và giá trị cùng dòng', () => {
  const r = parse(`CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
CĂN CƯỚC CÔNG DÂN
Số / No.: 079 201 012 345
Họ và tên / Full name:
NGUYỄN VĂN AN
Ngày sinh / Date of birth: 05/03/2001
Giới tính / Sex: Nam    Quốc tịch / Nationality: Việt Nam
Quê quán / Place of origin:
Hòa Bình, Thanh Chương, Nghệ An
Nơi thường trú / Place of residence:
12 Lê Lợi, Phường Bến Nghé, Quận 1, TP Hồ Chí Minh
Có giá trị đến / Date of expiry: 05/03/2026`);
  assert.equal(r.id, '079201012345'); assert.equal(r.type, 'CCCD');
  assert.equal(r.name, 'NGUYỄN VĂN AN'); assert.equal(r.dob, '05/03/2001');
  assert.equal(r.sex, 'Nam'); assert.equal(r.nation, 'Việt Nam');
  assert.match(r.origin, /Nghệ An/); assert.match(r.residence, /Hồ Chí Minh/);
  assert.equal(r.expiry, '05/03/2026');
});

test('CMND 9 số cũ', () => {
  const r = parse(`GIẤY CHỨNG MINH NHÂN DÂN
SỐ: 025 123 456
Họ tên: TRẦN THỊ BÍCH
Sinh ngày: 12-11-1990
Nguyên quán: Đống Đa, Hà Nội
Nơi ĐKHK thường trú: 5 Tây Sơn, Hà Nội`);
  assert.equal(r.id, '025123456'); assert.equal(r.type, 'CMND');
  assert.equal(r.name, 'TRẦN THỊ BÍCH'); assert.equal(r.dob, '12/11/1990');
});

test('khôi phục dấu họ / đệm khi OCR làm rơi dấu', () => {
  const r = parse('Họ và tên / Full name:\nNGUYEN THI HỒNG ÁNH\nSố: 079201012345');
  assert.equal(r.name, 'NGUYỄN THỊ HỒNG ÁNH');
  assert.equal(parse('Họ tên: LÊ VĂN TÂM').name, 'LÊ VĂN TÂM');
});

test('họ đọc sai dấu vẫn được sửa; tên đệm có dấu giữ nguyên', () => {
  assert.equal(parse('Họ tên: NGUYÊN VÂN ANH').name, 'NGUYỄN VÂN ANH');
  assert.equal(parse('Họ tên: TRẤN VĂN ĐỨC').name, 'TRẦN VĂN ĐỨC');
});

test('họ tên: nhãn bị OCR đọc sai hoặc mất vẫn tìm được', () => {
  assert.equal(parse('CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nCĂN CƯỚC\nSố: 079201012345\nHọ vả tên / Full name\nNGUYỄN VĂN AN\nNgày sinh: 05/03/2001').name, 'NGUYỄN VĂN AN');
  assert.equal(parse('CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nCĂN CƯỚC CÔNG DÂN\nSố: 079201012345\nNGUYỄN VĂN AN\nNgày sinh: 05/03/2001').name, 'NGUYỄN VĂN AN');
  assert.equal(parse('Full name: PHẠM THỊ LAN\nSố: 025123456').name, 'PHẠM THỊ LAN');
  assert.equal(parse('Họ và tên: Nguyễn Văn An\nSố: 025123456').name, 'NGUYỄN VĂN AN');
});

test('ảnh thật: CCCD mới chụp màn hình (mặt trước + mặt sau)', () => {
  const r = parse(`CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
CĂN CƯỚC
Số định danh cá nhân /Personal Identification number:
044085013062 ˆ-. “7
Họ, chữ đệm và tên /Full name: HÀ
HOÀNG QUỐC BẢO...
Ngày, tháng, năm sinh /Dateofbith: 2 Giới tính/ Sex Nam: -
| 13/07/1985 ôn cà na
i Quốc tịch /Nabonalty- Việt Nam VI Sợ
Mặt sau
Nơi cư trú /Place of residence: Thôn Cà TIE th NH la]
Hoàn Lão, Quảng Trị EM
Nơi đăng ký khai sinh / P/ace of birth: N
Hoàn Lão, Quảng Trị KIẾP,
—¬—= Ngày, tháng, năm cấp / Date of issue: in đu! Di
L¬ 12/09/2025 hy
Ngày, tháng, năm hết hạn /Da® ơ Expiy:
I1. 13/07/2045`);
  assert.equal(r.id, '044085013062'); assert.equal(r.name, 'HOÀNG QUỐC BẢO');
  assert.equal(r.dob, '13/07/1985'); assert.equal(r.sex, 'Nam'); assert.equal(r.nation, 'Việt Nam');
  assert.equal(r.residence, 'Thôn Cà, Hoàn Lão, Quảng Trị'); assert.equal(r.origin, 'Hoàn Lão, Quảng Trị');
  assert.equal(r.expiry, '13/07/2045');
});

test('ảnh thật: CCCD chụp bằng điện thoại, nhãn bị nhiễu', () => {
  const r = parse(`sé:x.: 051096015343
vs TH NT %
. TRẦN TẤN LƯỢNG
L Ngày sinh/Oate ofbith› 10/03/1996
Giớitính/ Sex: NÑGm quóc tịch/Na#onaniy. Việt Nam`);
  assert.equal(r.id, '051096015343'); assert.equal(r.name, 'TRẦN TẤN LƯỢNG');
  assert.equal(r.dob, '10/03/1996'); assert.equal(r.nation, 'Việt Nam');
});

test('mặt sau CCCD: không lấy ngày cấp làm ngày sinh, tên lấy từ MRZ', () => {
  const r = parse(`Ngày, tháng, năm /Date, morth, yea-O2/05/2022
+ ô Văn Huệ
: TRAN<<TAN<LUONG<<<<<<<<<<<<<<<`);
  assert.equal(r.name, 'TRẦN TAN LUONG'); assert.equal(r.dob, ''); assert.equal(r.sex, '');
});

test('ảnh thật 4: CCCD chụp nghiêng, nhãn hỏng nhiều', () => {
  const r = parse(`CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM XI
CĂN CƯỚC CÔNG DÂN. PT SA
Catren Identity Card 09s \
„2 036204006153 II
Họ và tên / fl narne - Ù
ĐINH THẾ QUANG gia ẤN
- Ngây snh/Osvorh 09/03/2004. v1}
Giớt6nh/ Soc Nam Quéc tịch A2fdrsf. Việt Nam.
Quê quản ¡ FXace ơf orig't Ạ nu Ván vi
Trực Chinh, Trực Ninh, Nam Định. Ư
Nơi thưởng trú / Place of 9 24 -
1 99 91 09V (gy3/2029 XE: TS ;i
v... Trực Chính, Trực Ninh, Nam Định S2`);
  assert.equal(r.id, '036204006153'); assert.equal(r.name, 'ĐINH THẾ QUANG');
  assert.equal(r.dob, '09/03/2004'); assert.equal(r.sex, 'Nam'); assert.equal(r.nation, 'Việt Nam');
  assert.equal(r.origin, 'Trực Chinh, Trực Ninh, Nam Định');
  assert.equal(r.residence, 'Trực Chính, Trực Ninh, Nam Định');
  assert.equal(r.expiry, '09/03/2029');
});

test('ảnh thật 6: số/ngày bị đọc thành chữ O/Q, ngày sinh sai', () => {
  const r = parse(`CỘNG HÒA XÃ HỘI CHỦ NGHÌA VIỆT NAM.  nux PO E
CĂN CƯỚC CÔNG DÀN. PS
Citizen ldentity Card wvŠx
sá/A¿: O56182004736
< " Họ và tên / Full name
xe mu NGUYEN THỊ NGOO HAO
~- Ngày sinh / Date ofbith› 4O/O7/1982
— Giớitính/Sex: NỮ Quốc tịch / No#o Việt Na
- Quê quán / Place of ongin
Diên Toàn, Diên Khánh, Khánh Hòa
Có giá tị đên:3Q/Q7/2042 Nơi thường trù "79oe  m90?0S9 [ hÔn Đồng Ut
>2. Diên Toàn, Diên Khánh, Khanh HÒa`);
  assert.equal(r.id, '056182004736'); assert.equal(r.type, 'CCCD');
  assert.equal(r.dob, '30/07/1982'); assert.equal(r.expiry, '30/07/2042');
  assert.equal(r.sex, 'Nữ'); assert.equal(r.nation, 'Việt Nam');
  assert.equal(r.origin, 'Diên Toàn, Diên Khánh, Khánh Hòa');
  assert.equal(r.residence, 'Diên Toàn, Diên Khánh, Khánh Hòa'); // "Khanh HÒa" → sửa dấu theo tên tỉnh
});

test('họ ngắn bị OCR đọc chữ thường ("Hồ THỊ KIM XUYÊN") vẫn giữ', () => {
  assert.equal(parse('Số / No.: 089304000123\nHọ và tên / Full name:\nHồ THỊ KIM XUYÊN\nNgày sinh / Date of birth: 05/02/2004').name, 'HỒ THỊ KIM XUYÊN');
  assert.equal(parse('Họ và tên / Full name:\nHÔ THỊ KIM XUYÊN').name, 'HỒ THỊ KIM XUYÊN');
  assert.equal(merge(R({ name: 'THỊ KIM XUYÊN' }), R({ name: 'HỒ THỊ KIM XUYÊN' })).name, 'HỒ THỊ KIM XUYÊN');
});

test('thường trú không có ":" sau nhãn; dòng 2 cùng hàng "Date of expiry"; rác cuối địa chỉ', () => {
  const r = parse(`Số / No.: 089304000123
Họ và tên / Full name:
HỒ THỊ KIM XUYÊN
Ngày sinh / Date of birth: 05/02/2004
Giới tính / Sex: Nữ Quốc tịch / Nationality: Việt Nam
Quê quán / Place of origin:
Khánh Hòa, Châu Phú, An Giang 4 P
Có giá trị đến 05/02/2029 Nơi thường trú / Place of residence Ấp Khánh Hưng
Date of expiry Khánh Hòa, Châu Phú, An Giang
ó, ni`);
  assert.equal(r.origin, 'Khánh Hòa, Châu Phú, An Giang');
  assert.equal(r.residence, 'Ấp Khánh Hưng, Khánh Hòa, Châu Phú, An Giang');
  assert.equal(r.expiry, '05/02/2029');
  assert.equal(parse('Nơi thường trú: 12 Lê Lợi, Phường 5').residence, '12 Lê Lợi, Phường 5'); // số phường cuối không bị cắt
});

test('tên "TRỊNH" (chỉ là họ) ở vị trí tên → "TRINH"; họ Trịnh giữ nguyên', () => {
  assert.equal(parse('Họ, chữ đệm và tên khai sinh / Full name:\nĐỖ THỊ MAI TRỊNH\nSố: 054194000111').name, 'ĐỖ THỊ MAI TRINH');
  assert.equal(parse('Họ và tên: TRỊNH VĂN AN').name, 'TRỊNH VĂN AN');
});

test('quê quán không lấy dư dòng nhãn thường trú bị OCR đọc hỏng', () => {
  const r = parse(`Số: 054304000222
Quê quán / Place of origin:
Hòa Phong, Tây Hòa,
Phú Yên...
trú Place đệ. Mỹ Thạnh Nam`);
  assert.equal(r.origin, 'Hòa Phong, Tây Hòa, Phú Yên');
});

test('địa chỉ: bỏ dòng rác và cụm từ rác ngắn giữa dòng', () => {
  assert.equal(parse('Quê quán / Place of origin:\nThành phố Thanh Hóa, Thanh Hóa\nri;, P ⁄3A8ZXÀ 2z Nó vo đi, 1 Ghi\nNơi thường trú: Phương Quới, De co Phương Bình, Phụng Hiệp, Hậu Giang').origin, 'Thành phố Thanh Hóa, Thanh Hóa');
  assert.equal(parse('Nơi thường trú: Phương Quới, De co Phương Bình, Phụng Hiệp, Hậu Giang').residence, 'Phương Quới, Phương Bình, Phụng Hiệp, Hậu Giang');
  assert.equal(parse('Nơi thường trú: Tổ dân phố 3, Thị xã Tân Uyên, Bình Dương').residence, 'Tổ dân phố 3, Thị xã Tân Uyên, Bình Dương');
  assert.equal(parse('Nơi thường trú: 12/4B Lê Lợi, Buôn Ma Thuột, Đắk Lắk').residence, '12/4B Lê Lợi, Buôn Ma Thuột, Đắk Lắk');
});

test('địa chỉ: rác dính cùng dòng sau tên tỉnh; sửa dấu tên tỉnh, thêm phẩy trước tỉnh', () => {
  assert.equal(parse('Quê quán / Place of origin:\nThành phố Thanh Hỏa Thanh Hóa ri;, P ⁄3A8ZXÀ 2z Nó vo đi, 1 Ghi').origin, 'Thành phố Thanh Hóa, Thanh Hóa');
  assert.equal(parse('Nơi thường trú: Hoằng Lộc, Thành Phố Thanh Hóa, Thanh Hỏa').residence, 'Hoằng Lộc, Thành Phố Thanh Hóa, Thanh Hóa');
  assert.equal(parse('Nơi thường trú: 12 Lê Lợi, Quận 1, Thành phố Hồ Chí Minh').residence, '12 Lê Lợi, Quận 1, Thành phố Hồ Chí Minh');
  assert.equal(parse('Nơi thường trú: 120/16A Bình Giã, Nguyễn An Ninh, Vũng Tàu, Bà Rịa - Vũng Tàu').residence, '120/16A Bình Giã, Nguyễn An Ninh, Vũng Tàu, Bà Rịa - Vũng Tàu');
});

test('ảnh thật 7: nhãn quê quán không có ":" ("Placo oforigis"); ghép 2 lượt bỏ đoạn rác kẹp giữa', () => {
  const a = parse(`IR ! | Quê quán / Placo oforigis Hoằng Lộc, Kha Xe,
th Thành \\ phố Thanh Hỏa Thanh Hóa \`“ ri;
: : P ⁄3A8ZXÀ th ñ ” : 2z NIÊN Nó vo j đi =
.E 1 Ghi TA .`);
  const b = parse(`_. Quê quán / Place of.origiru Hoằng Lộc,
Thành phố Thanh Hỏa, Thanh Hóa '
Ẻ Nơi tu ng trú / Placo of rosidnge: 120/1 6Ạ :
P.Nguyên An Ninh, TP.Vũng Tàu, B
¡TU úTx_5S--đ 1u HG`);
  assert.equal(b.origin, 'Hoằng Lộc, Thành phố Thanh Hóa, Thanh Hóa');
  assert.equal(b.residence, '120/1 6Ạ, P.Nguyên An Ninh, TP.Vũng Tàu');
  assert.equal(merge(a, b).origin, 'Hoằng Lộc, Thành phố Thanh Hóa, Thanh Hóa');
  // bản kia có thêm đoạn đầu mà bản này mất → bổ sung
  assert.equal(merge(R({ residence: 'Khánh Hòa, Châu Phú, An Giang' }), R({ residence: 'Ấp Khánh Hưng, Khánh Hòa, Châu Phú, An Giang' })).residence, 'Ấp Khánh Hưng, Khánh Hòa, Châu Phú, An Giang');
});

test('ảnh thật 8: quê quán ở nước ngoài ("Campuchi" sau nhãn "Place of origir")', () => {
  assert.equal(parse(`.e ‡ Place of origir 0a và
"Campuchi ” na
- Nơit trú Pece ơi res pBì
"Bình Mỹ, Châu Phú, An Giang`).origin, 'Campuchia');
  assert.equal(parse('"Quê quên: 2ecos don 220 l1\nNơi thường trú: Bình Mỹ, Châu Phú, An Giang').origin, ''); // nhãn "Quê quên" vẫn nhận, giá trị rác bị bỏ
});

test('quê quán: địa danh 2 chữ ngay sau nhãn ("An") + dòng kế', () => {
  assert.equal(parse('Quê quán / Place of origin: An\nBình, Phú Giáo, Bình Dương').origin, 'An Bình, Phú Giáo, Bình Dương');
});

test('completeOrigin: quê quán bị cắt được bổ sung theo đuôi nơi thường trú', () => {
  const { completeOrigin } = require('../site/js/cmnd-parse.js');
  const res = 'Số Nhà 12, Tổ 3, Ấp Bình Tiến, An Bình, Phú Giáo, Bình Dương';
  assert.equal(completeOrigin('Bình, Phú Giáo', res), 'An Bình, Phú Giáo, Bình Dương');
  assert.equal(completeOrigin('An Bình, Phú Giáo, Bình Dương', res), 'An Bình, Phú Giáo, Bình Dương');
  assert.equal(completeOrigin('Hòa Phong, Tây Hòa, Phú Yên', res), 'Hòa Phong, Tây Hòa, Phú Yên'); // không khớp → giữ
  assert.equal(completeOrigin('Campuchia', res), 'Campuchia');
  assert.equal(completeOrigin('Tân Bình, Phú Giáo', res), 'Tân Bình, Phú Giáo'); // khác chữ đầu → không đổi
});

test('ảnh thật 9: ":" rác cuối dòng nhãn thường trú; "Áp" → "Ấp"; nhãn "residonoe"', () => {
  assert.equal(parse('Nơi thường trú / Place ofresidenceÁp Khánh Hưng > ng x :\n; +. — .. Khánh Hòa, Châu Phú, An Giang NGỘ').residence, 'Ấp Khánh Hưng, Khánh Hòa, Châu Phú, An Giang');
  assert.equal(parse('” Nơi thường trú / Place of residonoe Ấp Khánh Hưng\nKhánh Hòa, Châu Phú, An Giang').residence, 'Ấp Khánh Hưng, Khánh Hòa, Châu Phú, An Giang');
});

test('hạn CCCD suy từ ngày sinh khi không đọc được (thẻ cấp 2016 – 06/2024)', () => {
  assert.equal(parse('CĂN CƯỚC CÔNG DÂN\nNgày sinh / Date of birth: 05/02/2004').expiry, '05/02/2029');
  assert.equal(parse('CĂN CƯỚC CÔNG DÂN\nNgày sinh / Date of birth: 10/03/1996').expiry, '');           // cấp 2016 → hạn 2021, cấp 2024 → 2036: không đoán
  assert.equal(parse('CĂN CƯỚC CÔNG DÂN\nNgày sinh / Date of birth: 05/03/2001').expiry, '');           // cấp trước/sau 2024 khác hạn → không đoán
  assert.equal(parse('CĂN CƯỚC\nSố định danh cá nhân\nNgày sinh: 05/02/2004').expiry, '');                // thẻ mẫu mới: luật khác
});

test('tên: rác sau "name :" → lấy dòng kế; tách chữ dính; bầu số CCCD theo từng chữ số', () => {
  assert.equal(parse('Họ và tên / “u# narne : «. ¡ l -\' là ï\nTRÀN VĂN ĐỨC XS 3°,\nNgày sinh / Oate be9!tf 14/06! 004').name, 'TRẦN VĂN ĐỨC XS'); // "XS" bị merge cắt sau
  assert.equal(merge(parse('Họ và tên / “u# narne : «. ¡ l -\' là ï\nTRÀN VĂN ĐỨC XS 3°,'), R({})).name, 'TRẦN VĂN ĐỨC');
  assert.equal(parse('Họvàtên/Fulname\' _ FT ‹ R\nHỒ THỊ KIMXUYÊN `').name, 'HỒ THỊ KIM XUYÊN');
  assert.equal(merge(parse('Họ và tên / Fulname: _ : .F “=> E\n—xÃXAR THỊ KIM XUYÊN “ti=-'), parse('Họvàtên/Fulname\' _ FT ‹ R\nHỒ THỊ KIMXUYÊN `')).name, 'HỒ THỊ KIM XUYÊN');
  const { voteId } = require('../site/js/cmnd-parse.js');
  assert.equal(voteId(['089204003678', '089204005671', '089204005678']), '089204005678');
  assert.equal(voteId(['089204003678', '089204005671']), ''); // < 3 lượt → không bầu
});

test('thẻ Căn cước mới: nhãn "Họ chứ độm vớ tên" (OCR của "Họ, chữ đệm và tên"); rác chữ thường sau tên', () => {
  const t = '| x3) 089204000444 34 |\ns ! Họ chứ độm vớ tên Nhaá sin© (7.2 name. -\n. ĐOÀN HỮU LỘC Gì\n. 3 Ỷ Ngày. tháng. nêm sinh / Oom, cơ ngờ Giới 6nh clSos !';
  assert.equal(parse(t).name, 'ĐOÀN HỮU LỘC');
  assert.equal(parse('CĂN CƯỚC\n089204000444\nkhông nhãn\n. ĐOÀN HỮU LỘC Gì\n22/05/2004').name, 'ĐOÀN HỮU LỘC'); // mất nhãn → đoán theo dòng in hoa
});

test('bỏ chữ rác 1 ký tự trước tên', () => {
  assert.equal(parse('Họ và tên / Full name\nÌ NGUYÊN THỊ NGỌC HẢO\nSố: 056182004736').name, 'NGUYỄN THỊ NGỌC HẢO');
});

test('mã QR CCCD và suy hạn dùng', () => {
  const { parseQr, expiryFrom } = require('../site/js/cmnd-parse.js');
  const q = parseQr('051096015343|212345678|Trần Tấn Lượng|10031996|Nam|Tdp An Thường, Phổ Hòa, Thị xã Đức Phổ, Quảng Ngãi|10032021');
  assert.equal(q.id, '051096015343'); assert.equal(q.name, 'TRẦN TẤN LƯỢNG'); assert.equal(q.dob, '10/03/1996');
  assert.equal(q.sex, 'Nam'); assert.match(q.residence, /Quảng Ngãi$/); assert.equal(q.issued, '10/03/2021');
  assert.equal(parseQr('https://example.com'), null);
  assert.equal(expiryFrom('10/03/1996', '10/03/2021'), '10/03/2036'); // cấp năm 25 tuổi → đến 40
  assert.equal(expiryFrom('09/03/2004', '01/05/2021'), '09/03/2029'); // 17 tuổi → đến 25
  assert.equal(expiryFrom('30/07/1982', '01/05/2021'), '30/07/2042'); // 38 tuổi (trong 2 năm trước 40) → đến 60
  assert.equal(expiryFrom('01/01/1950', '01/05/2021'), 'Không thời hạn');
});

test('QR: địa chỉ có ô trống giữa hai dấu phẩy', () => {
  const { parseQr } = require('../site/js/cmnd-parse.js');
  assert.equal(parseQr('044085013062|194250642|Hoàng Quốc Bảo|13071985|Nam|Thôn Cà, Hoàn Lão, , Quảng Trị|12092025').residence, 'Thôn Cà, Hoàn Lão, Quảng Trị');
});

// ---- ghép lượt đọc cả ảnh + lượt đọc vùng thẻ đã cắt ----
const R = o => Object.assign({ id: '', name: '', dob: '', sex: '', nation: '', origin: '', residence: '', expiry: '', type: '' }, o);

test('merge: tên rác ở lượt 1 được thay bằng tên đọc được ở lượt cắt, số 12 chữ số thắng', () => {
  const r = merge(R({ id: '089204001230', name: 'TKƯƠI NẠ', dob: '14/06/2296', sex: 'Nam' }),
    R({ id: '089204005678', name: 'TRẦN VĂN ĐỨC PL', dob: '14/06/2004', nation: 'Nafionaliy VÌ' }));
  assert.equal(r.id, '089204005678'); assert.equal(r.name, 'TRẦN VĂN ĐỨC'); // bỏ "PL" rác cuối tên
  assert.equal(r.dob, '14/06/2004'); assert.equal(r.nation, 'Việt Nam');  // quốc tịch rác → CCCD nên là Việt Nam
  assert.equal(merge(R({ nation: 'Nafionaliy VÌ' }), R({})).nation, '');  // không có số giấy tờ → bỏ rác
});

test('quốc tịch rác ("và") → Việt Nam khi có số giấy tờ', () => {
  assert.equal(parse('Số: 054194000111\nQuốc tịch / Nationality: và').nation, 'Việt Nam');
  assert.equal(parse('Quốc tịch: và').nation, '');
});

test('địa chỉ 2 dòng: dòng 2 nằm cùng hàng với "Có giá trị đến"; bỏ rác chữ thường ở đầu', () => {
  const r = parse(`Số: 074307000555
Họ và tên / Full name:
VÕ THỊ NGỌC LAN
Ngày sinh / Date of birth: 09/09/2007
Giới tính / Sex: Nữ Quốc tịch / Nationality: Việt Nam
Quê quán / Place of origin:
hi á Tân Hiệp, Thị xã Tân Uyên,
Bình Dương
Nơi thường trú / Place of residence: Tổ 2, Khu Phố Tân Phú, Tân
Có giá trị đến: 09/09/2032 Bình, Dĩ An, Bình Dương`);
  assert.equal(r.origin, 'Tân Hiệp, Thị xã Tân Uyên, Bình Dương');
  assert.equal(r.residence, 'Tổ 2, Khu Phố Tân Phú, Tân Bình, Dĩ An, Bình Dương');
  assert.equal(r.expiry, '09/09/2032');
  assert.equal(parse('Nơi thường trú: Thôn Mỹ Thạnh Nam, Hòa Phong,\nDate of expiry 11/04/2029 Tây Hòa, Phú Yên').residence, 'Thôn Mỹ Thạnh Nam, Hòa Phong, Tây Hòa, Phú Yên');
});

test('merge: hòa thì giữ lượt 1 (tên hợp lệ không bị đổi dấu bởi lượt cắt)', () => {
  const r = merge(R({ name: 'LƯU THUÝ HIỀN', dob: '15/04/1990', nation: 'Việt Nam' }), R({ name: 'LƯU THUÝ HIÊN', dob: '00/04/1990' }));
  assert.equal(r.name, 'LƯU THUÝ HIỀN'); assert.equal(r.dob, '15/04/1990'); assert.equal(r.nation, 'Việt Nam');
});

test('merge: hạn lấy nhầm ngày cấp bị bỏ; hạn sai ngày được suy lại từ ngày sinh', () => {
  assert.equal(merge(R({ dob: '03/04/1983', expiry: '30/11/2022' }), R({ expiry: '03/04/2043' })).expiry, '03/04/2043');
  assert.equal(merge(R({ dob: '12/10/2004', expiry: '02/10/2029' }), R({})).expiry, '12/10/2029');
  assert.equal(merge(R({ dob: '03/04/1983', expiry: '30/11/2022' }), R({})).expiry, '');
});

test('merge: giới tính suy từ chữ số 4 của số CCCD khi OCR không đọc được', () => {
  assert.equal(merge(R({ id: '054194000111' }), R({})).sex, 'Nữ');
  assert.equal(merge(R({ id: '089204000444' }), R({})).sex, 'Nam');
  assert.equal(merge(R({ id: '054194000111', sex: 'Nam' }), R({})).sex, 'Nam'); // OCR đã đọc thì giữ
});

test('cardBox: bao quanh các chữ đọc chắc; null khi gần bằng cả ảnh hoặc quá ít chữ', () => {
  const w = (x, y) => ({ text: 'Họ', confidence: 90, bbox: { x0: x, y0: y, x1: x + 60, y1: y + 20 } });
  const words = []; for (let i = 0; i < 20; i++) words.push(w(500 + (i % 5) * 100, 700 + Math.floor(i / 5) * 60));
  const b = cardBox(words, 2000, 2000);
  assert.ok(b && b.x < 500 && b.y < 700 && b.x + b.w > 960 && b.w < 700);
  assert.equal(cardBox(words.slice(0, 3), 2000, 2000), null);
  const wide = []; for (let i = 0; i < 20; i++) wide.push(w((i % 10) * 190, Math.floor(i / 10) * 180));
  assert.equal(cardBox(wide, 1950, 220), null); // chữ phủ gần cả ảnh → không cần cắt
});

test('qrWindows: cả ảnh đầu tiên, vùng góc trên-phải thẻ, mọi cửa sổ nằm trong ảnh', () => {
  const W = 1920, H = 2560, box = { x: 400, y: 900, w: 900, h: 1100 };
  const wins = require('../site/js/cmnd-parse.js').qrWindows(W, H, box);
  assert.deepEqual(wins[0], [0, 0, W, H]);
  const [x, y, w] = wins[1]; assert.ok(x >= box.x + box.w * .4 && y < box.y && w < box.w); // lệch sang phải, bắt đầu từ mép trên thẻ
  for (const [a, b, c, d] of wins) assert.ok(a >= 0 && b >= 0 && c > 0 && d > 0 && a + c <= W && b + d <= H);
  assert.ok(wins.length > 5 && wins.length < 120);
  assert.equal(require('../site/js/cmnd-parse.js').qrWindows(W, H, null)[1][2] < W, true); // không có box → thẳng sang cửa sổ trượt
});
