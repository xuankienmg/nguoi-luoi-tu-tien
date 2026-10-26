// Run from the repository root: node tools/final_realm_checks/client.cjs
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const context = vm.createContext({ Image: class {}, localStorage: { getItem: () => null } });
for (const name of ['fire-fx-data', 'water-wood-fx-data', 'four-elements-fx-data', 'fire-fx', 'skills'])
  vm.runInContext(fs.readFileSync(path.join(root, `wwwroot/js/${name}.js`), 'utf8'), context, { filename: name });
let checks = 0;
const hashes = new Set();
function icon(relative) {
  const png = fs.readFileSync(path.join(root, 'wwwroot', relative));
  assert.equal(png.subarray(0, 8).toString('hex'), '89504e470d0a1a0a', relative);
  assert.equal(png.readUInt32BE(16), 128, relative);
  assert.equal(png.readUInt32BE(20), 128, relative);
  hashes.add(crypto.createHash('sha256').update(png).digest('hex'));
  checks++;
}
for (const element of ['hoa', 'thuy', 'moc', 'tho', 'loi', 'bang', 'gio'])
  for (const realm of ['do_kiep', 'chan_tien'])
    for (const tree of ['active', 'passive']) {
      const id = `${element}_${realm}_${tree}`;
      const mapped = vm.runInContext(`skIcon({id:${JSON.stringify(id)}})`, context);
      assert.equal(mapped, `img/final_realms/skills/${id}.png`);
      icon(mapped);
      if (tree === 'active') {
        const spec = vm.runInContext(`fireFxSpec(${JSON.stringify(id)}, 9)`, context);
        assert.ok(spec?.src && fs.existsSync(path.join(root, 'wwwroot', spec.src)), `${id}: missing VFX sheet`);
        checks++;
      }
    }
for (const prefix of ['dk', 'ct'])
  for (const piece of ['kiem','chau','mu','ao','gang','quan','giay','day','phu','nhan'])
    icon(`img/final_realms/gear/${prefix}_${piece}.png`);
assert.equal(hashes.size, 48, 'All forty-eight production icons must be distinct');
console.log(`PASS: ${checks} client checks (PNG format, dimensions, unique artwork, real skill mappings and VFX sources).`);
