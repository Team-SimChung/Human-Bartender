'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const zlib = require('node:zlib');
const assert = require('node:assert/strict');
const project = path.resolve(__dirname, '../..');
const output = path.join(project, 'Documentation/Gimmick_UI_Web_2026-11-03.assets.json');
const blobHash = bytes => crypto.createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex');
function rgbaHash(bytes) {
    const width=bytes.readUInt32BE(16),height=bytes.readUInt32BE(20);
    assert.equal(bytes[24],8,'Pixel comparison requires 8-bit PNG.');
    assert.equal(bytes[25],6,'Pixel comparison requires RGBA PNG.');
    assert.equal(bytes[28],0,'Pixel comparison requires a non-interlaced PNG.');
    const chunks=[];
    for(let at=8;at<bytes.length;){const length=bytes.readUInt32BE(at);if(bytes.toString('ascii',at+4,at+8)==='IDAT')chunks.push(bytes.subarray(at+8,at+8+length));at+=length+12;}
    const raw=zlib.inflateSync(Buffer.concat(chunks)),stride=width*4,pixels=Buffer.alloc(stride*height);let at=0;
    for(let y=0;y<height;y++){
        const filter=raw[at++];assert(filter<=4,'Unknown PNG filter.');
        for(let x=0;x<stride;x++){
            const left=x>=4?pixels[y*stride+x-4]:0,up=y?pixels[(y-1)*stride+x]:0,corner=y&&x>=4?pixels[(y-1)*stride+x-4]:0;
            let value=raw[at++];
            if(filter===1)value+=left;
            if(filter===2)value+=up;
            if(filter===3)value+=Math.floor((left+up)/2);
            if(filter===4){const p=left+up-corner,a=Math.abs(p-left),b=Math.abs(p-up),c=Math.abs(p-corner);value+=a<=b&&a<=c?left:b<=c?up:corner;}
            pixels[y*stride+x]=value&255;
        }
    }
    return crypto.createHash('sha256').update(`${width}x${height}\0`).update(pixels).digest('hex');
}
let report;
if (process.argv.includes('--refresh')) {
    const source = path.join(project, 'Temp/GimmickWebReference');
    const tree = JSON.parse(fs.readFileSync(path.join(source, 'tree.json'), 'utf8'));
    const text = fs.readFileSync(path.join(source, 'data/runtime/game-data.js'), 'utf8').replace(/^\uFEFF/, '');
    const data = JSON.parse(text.slice(text.indexOf(' = ') + 3).trim().replace(/;$/, ''));
    const selected = new Set(tree.tree.filter(v => v.type === 'blob' && v.path.startsWith('bar-playtest/assets/01.ArtResources/') && v.path.includes('/02.Minigames/') && /\.(png|jpg|webp)$/.test(v.path)).map(v => v.path));
    for (const [key, value] of Object.entries(data.assets))
        if (/^(gimmick|mix_)/.test(key) || ['item_beer', 'item_opener'].includes(key)) selected.add('bar-playtest/' + value.src);
    selected.add('bar-playtest/assets/01.ArtResources/03.Crafting/04.Minigames/03.pour/cup3.png');
    const byPath = new Map(tree.tree.map(v => [v.path, v]));
    report = {
        repository: 'junseo874/junseo874.github.io', commit: tree.sha,
        hash: 'Git blob SHA-1 (header + original bytes)',
        assets: [...selected].sort().map(sourcePath => {
            const original = byPath.get(sourcePath); assert(original, `Missing source tree entry: ${sourcePath}`);
            const local = sourcePath === 'bar-playtest/assets/items/item-beer.png'
                ? 'Assets/01.Game/03.Content/01.ArtResources/03.Crafting/04.Minigames/04.Open/item_beer_open.png'
                : sourcePath.replace('bar-playtest/assets/01.ArtResources/', 'Assets/01.Game/03.Content/01.ArtResources/');
            return {source: sourcePath, local, sha: original.sha, previewOnly: sourcePath.includes('/WebGenerated/Combined/')};
        }),
    };
}
else report = JSON.parse(fs.readFileSync(output, 'utf8'));
const counts = {identical: 0, identicalPixels: 0, different: 0, missingPreview: 0, missingRuntime: 0};
for (const item of report.assets) {
    const file = path.join(project, item.local);
    if(process.argv.includes('--refresh') && fs.existsSync(file) && blobHash(fs.readFileSync(file))!==item.sha){
        const original=path.join(project,'Temp/GimmickWebReference',path.basename(item.source));
        if(fs.existsSync(original)&&blobHash(fs.readFileSync(original))===item.sha)item.pixelSha=rgbaHash(fs.readFileSync(original));
    }
    item.status = fs.existsSync(file) ? blobHash(fs.readFileSync(file)) === item.sha ? 'identical'
        : item.pixelSha && rgbaHash(fs.readFileSync(file))===item.pixelSha ? 'identicalPixels' : 'different'
        : item.previewOnly ? 'missingPreview' : 'missingRuntime';
    counts[item.status]++;
}
report.counts = counts;
if (process.argv.includes('--refresh')) fs.writeFileSync(output, JSON.stringify(report, null, 2) + '\n');
assert.equal(counts.different, 0, 'Some local original images differ from the web reference.');
assert.equal(counts.missingRuntime, 0, 'Runtime gimmick artwork is missing.');
console.log('GIMMICK_ASSETS_OK: ' + JSON.stringify(counts));
