const test = require('node:test'), assert = require('node:assert');
const { parse } = require('../site/js/cmnd-parse.js');

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
  assert.equal(r.residence, 'Diên Toàn, Diên Khánh, Khanh Hòa');
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
