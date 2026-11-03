/* Author serialized Unity UI without opening/saving the user's editor scenes.
 * Reference: junseo874/junseo874.github.io @ 24022cdec49a23200d5500a3e8deef931267206d.
 * node Tools/UI/align-gimmick-ui.cjs --apply | --check
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const project = path.resolve(__dirname, '../..');
const artRoot = 'Assets/01.Game/03.Content/01.ArtResources/05.UI/';
const imageGuid = 'fe87c0e1cc204ed48ad3b37840f39efc';
const textGuid = 'f4688fdb7df04437aeb418b961361dc5';
const ref = id => `{fileID: ${id}}`;
const color = hex => {
    const n = hex.match(/../g).map(v => parseInt(v, 16) / 255);
    return `{r: ${n[0]}, g: ${n[1]}, b: ${n[2]}, a: ${n[3] ?? 1}}`;
};
const vector = (x, y) => `{x: ${x}, y: ${y}}`;
const escape = v => v.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

class UnityFile {
    constructor(relative) {
        this.relative = relative;
        this.file = path.join(project, relative);
        this.original = fs.readFileSync(this.file, 'utf8');
        this.newline = this.original.includes('\r\n') ? '\r\n' : '\n';
        const source = this.original.replaceAll('\r\n', '\n');
        this.prefix = source.slice(0, source.indexOf('--- !u!'));
        this.blocks = [...source.matchAll(/^--- !u!(\d+) &(-?\d+)[^\n]*\n[\s\S]*?(?=^--- !u!|$(?![\s\S]))/gm)]
            .map(m => ({ type: m[1], id: m[2], text: m[0] }));
        this.byId = new Map(this.blocks.map(b => [b.id, b]));
        this.owned = new Set();
        this.next = 930000000;
    }
    get(id) { const b = this.byId.get(String(id)); assert(b, `${this.relative}: missing ${id}`); return b; }
    field(b, key) { return b.text.match(new RegExp(`^  ${escape(key)}: (.*)$`, 'm'))?.[1]; }
    set(b, key, value) {
        const re = new RegExp(`^  ${escape(key)}:.*$`, 'm');
        if (re.test(b.text)) b.text = b.text.replace(re, () => `  ${key}: ${value}`);
        else b.text += `  ${key}: ${value}\n`;
    }
    id(b, key) { return this.field(b, key)?.match(/fileID: (-?\d+)/)?.[1]; }
    go(b) { return this.get(this.id(b, 'm_GameObject')); }
    name(b) { return this.field(b, 'm_Name')?.replace(/^'(.*)'$/, '$1'); }
    find(name) { const items = this.blocks.filter(b => b.type === '1' && this.name(b) === name); assert.equal(items.length, 1, `Expected unique ${name} in ${this.relative}`); return items[0]; }
    components(go) { return [...go.text.matchAll(/^  - component: \{fileID: (-?\d+)\}/gm)].map(m => this.get(m[1])); }
    component(go, guid) { return this.components(go).find(b => this.field(b, 'm_Script')?.includes(guid)); }
    rect(go) { return this.components(go).find(b => b.type === '224'); }
    script(name) { return this.blocks.find(b => b.text.includes(`m_EditorClassIdentifier: Assembly-CSharp::${name}\n`)); }
    children(rect) {
        const list = rect.text.match(/^  m_Children:.*\n(?:  - \{fileID: -?\d+\}\n)*/m)?.[0] || '';
        return [...list.matchAll(/fileID: (-?\d+)/g)].map(m => m[1]);
    }
    setChildren(rect, ids) {
        rect.text = rect.text.replace(/^  m_Children:.*\n(?:  - \{fileID: -?\d+\}\n)*/m,
            () => ids.length ? `  m_Children:\n${ids.map(id => `  - ${ref(id)}\n`).join('')}` : '  m_Children: []\n');
    }
    parent(rect, parent) {
        const old = this.id(rect, 'm_Father');
        if (old && old !== '0') this.setChildren(this.get(old), this.children(this.get(old)).filter(id => id !== rect.id));
        this.set(rect, 'm_Father', ref(parent.id));
        this.setChildren(parent, [...this.children(parent).filter(id => id !== rect.id), rect.id]);
    }
    place(rect, x, y, w, h) {
        this.set(rect, 'm_AnchorMin', vector(0, 1)); this.set(rect, 'm_AnchorMax', vector(0, 1));
        this.set(rect, 'm_Pivot', vector(0, 1)); this.set(rect, 'm_AnchoredPosition', vector(x, -y));
        this.set(rect, 'm_SizeDelta', vector(w, h)); this.set(rect, 'm_LocalScale', '{x: 1, y: 1, z: 1}');
    }
    center(rect, x, y, w, h, scale = 1) {
        this.set(rect, 'm_AnchorMin', vector(.5, .5)); this.set(rect, 'm_AnchorMax', vector(.5, .5));
        this.set(rect, 'm_Pivot', vector(.5, .5)); this.set(rect, 'm_AnchoredPosition', vector(x, y));
        this.set(rect, 'm_SizeDelta', vector(w, h)); this.set(rect, 'm_LocalScale', `{x: ${scale}, y: ${scale}, z: 1}`);
    }
    allocate() { while (this.byId.has(String(this.next))) this.next++; return String(this.next++); }
    add(type, body) {
        const id = this.allocate(); const b = {type: String(type), id, text: `--- !u!${type} &${id}\n${body.trimEnd()}\n`};
        this.blocks.push(b); this.byId.set(id, b); this.owned.add(id); return b;
    }
    clone(prototype, go) {
        const body = prototype.text.slice(prototype.text.indexOf('\n') + 1).replace(/^  m_GameObject:.*$/m, `  m_GameObject: ${ref(go.id)}`);
        const component = this.add(prototype.type, body);
        go.text = go.text.replace(/(  m_Component:\n(?:  - component:.*\n)*)/, `$1  - component: ${ref(component.id)}\n`);
        return component;
    }
    node(name, parent) {
        let go = this.blocks.find(b => b.type === '1' && this.name(b) === name);
        if (go) { this.owned.add(go.id);for(const b of this.components(go))this.owned.add(b.id);return {go, rect: this.rect(go)}; }
        go = this.add(1, `GameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n  m_Layer: 5\n  m_Name: ${name}\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1`);
        const rect = this.add(224, `RectTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: ${ref(go.id)}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: {x: 0, y: 1}\n  m_AnchorMax: {x: 0, y: 1}\n  m_AnchoredPosition: {x: 0, y: 0}\n  m_SizeDelta: {x: 0, y: 0}\n  m_Pivot: {x: 0, y: 1}`);
        go.text = go.text.replace('  m_Component:\n', `  m_Component:\n  - component: ${ref(rect.id)}\n`);
        this.parent(rect, parent); return {go, rect};
    }
    graphic(name, parent, prototype) {
        const node = this.node(name, parent);
        if (!this.components(node.go).some(b => b.type === '222')) {
            const renderer = this.add(222, `CanvasRenderer:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: ${ref(node.go.id)}\n  m_CullTransparentMesh: 1`);
            node.go.text = node.go.text.replace(/(  m_Component:\n(?:  - component:.*\n)*)/, `$1  - component: ${ref(renderer.id)}\n`);
        }
        node.graphic = this.component(node.go, this.field(prototype, 'm_Script').match(/guid: ([\da-f]+)/)[1]) || this.clone(prototype, node.go);
        this.set(node.graphic, 'm_RaycastTarget', 0); return node;
    }
    text(name, parent, prototype, value, x, y, w, h, size, hex, align = 1) {
        const n = this.graphic(name, parent, prototype); this.place(n.rect, x, y, w, h);
        for (const [key, v] of Object.entries({m_text: JSON.stringify(value),m_fontSize:size,m_fontSizeBase:size,m_fontSizeMin:10,m_HorizontalAlignment:align,m_VerticalAlignment:256,m_TextWrappingMode:0,m_Color:color(hex),m_fontColor:color(hex)})) this.set(n.graphic, key, v);
        return n;
    }
    image(name, parent, prototype, sprite, hex, x, y, w, h) {
        const n = this.graphic(name, parent, prototype); this.place(n.rect, x, y, w, h);
        for (const [key, value] of Object.entries({m_Sprite:sprite || ref(0),m_Type:0,m_PreserveAspect:0,m_Color:color(hex)})) this.set(n.graphic,key,value);
        return n;
    }
    stretch(rect) { this.set(rect,'m_AnchorMin',vector(0,0));this.set(rect,'m_AnchorMax',vector(1,1));this.set(rect,'m_AnchoredPosition',vector(0,0));this.set(rect,'m_SizeDelta',vector(0,0));this.set(rect,'m_Pivot',vector(.5,.5)); }
    serialize() {
        for(const id of this.owned)this.get(id).text=this.get(id).text.replace(/[ \t]+$/gm,'');
        return (this.prefix + this.blocks.map(b => b.text).join('')).replaceAll('\n', this.newline);
    }
    validate() {
        assert.equal(this.byId.size, this.blocks.length, 'Duplicate serialized IDs');
        for (const b of this.blocks.filter(v => v.type === '224')) {
            for (const id of this.children(b)) {
                const child = this.get(id);
                if (child.text.split('\n',1)[0].includes(' stripped')) continue;
                assert.equal(this.id(child,'m_Father'),b.id,`${this.relative}: parent mismatch for ${id}`);
            }
        }
    }
}

const usedSprites = new Set();
function sprite(relative, namePattern) {
    const file = path.join(project, artRoot, relative);
    const meta = fs.readFileSync(file + '.meta', 'utf8').replaceAll('\r\n','\n');
    const guid = meta.match(/^guid: (\w+)/m)[1];
    const entries = [...meta.matchAll(/    - serializedVersion: 2\n      name: ([^\n]+)\n[\s\S]*?        width: (\d+)\n        height: (\d+)[\s\S]*?      internalID: (-?\d+)/g)]
        .map(m => ({name:m[1],area:Number(m[2])*Number(m[3]),id:m[4]}));
    let best = entries.filter(v => !namePattern || namePattern.test(v.name)).sort((a,b)=>b.area-a.area)[0];
    if (!best && /spriteMode: 1\b/.test(meta)) best = {id:'21300000'};
    assert(best, `No sprite in ${relative}`); usedSprites.add(artRoot+relative);
    return `{fileID: ${best.id}, guid: ${guid}, type: 3}`;
}

function commonHud(file) {
    const hud = file.script('CraftGimmickHud'); assert(hud);
    const root = file.rect(file.get(file.id(hud,'root')));
    file.center(root,0,0,1280,720);
    const textPrototype = file.get(file.id(hud,'subjectText'));
    const button = file.get(file.id(hud,'nextButton'));
    const buttonGo = file.go(button), imagePrototype = file.get(file.id(button,'m_TargetGraphic'));
    const q = file.image('Pour Quantity Readout',root,imagePrototype,sprite('03.Crafting/02.Minigames/02.Pour/WebGenerated/Web_GRD_pour_top_readout_background.png'),'ffffffff',26,28,300,75);
    file.text('Pour Target Caption',q.rect,textPrototype,'목표량',0,0,110,20,14,'a8bdc8');
    const target = file.text('Pour Target Value',q.rect,textPrototype,'???',0,25,130,38,28,'f0d193');
    file.text('Pour Current Caption',q.rect,textPrototype,'현재량',154,0,110,20,14,'a8bdc8');
    const current = file.text('Pour Current Value',q.rect,textPrototype,'???',154,25,140,38,28,'85eff0');
    const ingredient = file.node('Pour Ingredient Readout',root); file.place(ingredient.rect,26,622,500,70);
    file.text('Pour Ingredient Caption',ingredient.rect,textPrototype,'재료 이름',0,0,420,20,14,'a8bdc8');
    file.parent(file.rect(file.go(textPrototype)),ingredient.rect); file.place(file.rect(file.go(textPrototype)),0,24,500,44);
    for(const [key,value] of Object.entries({m_fontSize:28,m_fontSizeBase:28,m_HorizontalAlignment:1,m_Color:color('f0d193'),m_fontColor:color('f0d193'),m_RaycastTarget:0})) file.set(textPrototype,key,value);
    const tilt = file.node('Pour Tilt Meter',root); file.place(tilt.rect,520,28,240,60);
    file.text('Pour Tilt Caption',tilt.rect,textPrototype,'기울이기',0,0,240,24,16,'b9c4cf',2);
    const track=file.image('Pour Tilt Track',tilt.rect,imagePrototype,null,'19202b',0,33,240,16);
    const range=file.image('Pour Flow Range',track.rect,imagePrototype,null,'45644b99',0,0,0,16);
    const warning=file.image('Pour Tilt Warning',track.rect,imagePrototype,null,'a8854d77',0,0,0,16);
    for(const [n,start,end] of [[range,.375,.625],[warning,.625,1]]) { file.set(n.rect,'m_AnchorMin',vector(start,0));file.set(n.rect,'m_AnchorMax',vector(end,1));file.set(n.rect,'m_SizeDelta',vector(0,0));file.set(n.rect,'m_AnchoredPosition',vector(0,0));file.set(n.rect,'m_Pivot',vector(.5,.5)); }
    const marker=file.image('Pour Tilt Marker',track.rect,imagePrototype,null,'cad7e0',0,0,4,26);file.set(marker.rect,'m_AnchorMin',vector(0,.5));file.set(marker.rect,'m_AnchorMax',vector(0,.5));file.set(marker.rect,'m_Pivot',vector(.5,.5));
    const nextRect=file.rect(buttonGo);file.place(nextRect,1038,655,220,47);
    file.set(imagePrototype,'m_Sprite',sprite('03.Crafting/02.Minigames/02.Pour/WebGenerated/Web_BTN_pour_finish_gimmick_finish_background.png'));file.set(imagePrototype,'m_Color',color('ffffffff'));file.set(imagePrototype,'m_Type',0);
    const labelGo=file.children(nextRect).map(id=>file.go(file.get(id))).find(go=>file.component(go,textGuid));assert(labelGo);
    const label=file.component(labelGo,textGuid);file.stretch(file.rect(labelGo));file.set(label,'m_fontSize',14);file.set(label,'m_fontSizeBase',14);file.set(label,'m_text',JSON.stringify('따르기 마치기 →'));file.set(label,'m_Color',color('eceddf'));file.set(label,'m_fontColor',color('eceddf'));file.set(label,'m_RaycastTarget',0);
    const border=file.image('Pour Finish Border',nextRect,imagePrototype,sprite('03.Crafting/02.Minigames/02.Pour/WebGenerated/Web_GRD_pour_finish_gimmick_finish_border.png'),'ffffffff',0,0,220,47);file.stretch(border.rect);
    const timer=file.rect(file.get(file.id(hud,'timeRoot')));file.set(timer,'m_AnchorMin',vector(1,1));file.set(timer,'m_AnchorMax',vector(1,1));file.set(timer,'m_Pivot',vector(1,1));file.set(timer,'m_AnchoredPosition',vector(-73,-54));file.set(timer,'m_SizeDelta',vector(138,40));file.set(timer,'m_LocalScale','{x: 1, y: 1, z: 1}');
    const time=file.get(file.id(hud,'timeText'));file.place(file.rect(file.go(time)),48,0,90,40);file.set(time,'m_fontSize',33);file.set(time,'m_fontSizeBase',33);file.set(time,'m_RaycastTarget',0);
    for(const id of file.children(timer)){const go=file.go(file.get(id)),im=file.component(go,imageGuid);if(im){file.place(file.rect(go),0,-4,43,48);file.set(im,'m_Sprite',sprite('03.Crafting/02.Minigames/01.Stir/ICN_sitr_time.png'));file.set(im,'m_PreserveAspect',1);file.set(im,'m_RaycastTarget',0);}}
    const progress=file.get(file.id(hud,'progressText'));file.place(file.rect(file.go(progress)),26,28,320,60);file.set(progress,'m_fontSize',28);file.set(progress,'m_RaycastTarget',0);
    const ok=file.get(file.id(hud,'okMark'));file.place(file.rect(ok),712,166,240,50);const okText=file.component(ok,textGuid);if(okText){file.set(okText,'m_text','PERFECT');file.set(okText,'m_fontSize',32);file.set(okText,'m_fontSizeBase',32);file.set(okText,'m_fontColor',color('b6fff0'));file.set(okText,'m_RaycastTarget',0);}
    for(const [key,id] of Object.entries({quantityRoot:q.go.id,targetValueText:target.graphic.id,currentValueText:current.graphic.id,ingredientRoot:ingredient.go.id,tiltRoot:tilt.go.id,tiltMarker:marker.rect.id,tiltFlowRange:range.rect.id,tiltWarningRange:warning.rect.id,nextButtonLabel:label.id}))file.set(hud,key,ref(id));
    file.set(hud,'timeInset',vector(73,54));
}

function stir(file) {
    const layout=file.script('StirLayoutView');assert(layout);
    const at=(key,x,y,w,h)=>file.center(file.get(file.id(layout,key)),(x+w/2-640)*.75,(360-y-h/2)*.75,w*.75,h*.75);
    at('leftFrame',0,0,1280,720);at('characterViewport',34,29,388,664);at('closeupFrame',350,0,187,325);at('closeupViewport',351,1,185,323);at('resultGauge',496,632,708,30);at('elapsedIcon',1069,50,43,48);at('elapsedText',1117,54,90,40);at('startHint',500,583,700,28);
    for(const key of ['rightFrame','grid'])file.set(file.go(file.get(file.id(layout,key))),'m_IsActive',0);
    const backgroundRect=file.get(file.id(layout,'leftFrame'));
    const background=file.component(file.go(backgroundRect),imageGuid);file.set(background,'m_Sprite',sprite('03.Crafting/02.Minigames/01.Stir/BG_stir.png'));file.set(background,'m_Type',0);file.set(background,'m_PreserveAspect',0);file.set(background,'m_Color',color('ffffffff'));
    const backgroundParent=file.get(file.id(backgroundRect,'m_Father'));
    file.setChildren(backgroundParent,[backgroundRect.id,...file.children(backgroundParent).filter(id=>id!==backgroundRect.id)]);
    const closeup=file.rect(file.find('Stir Closeup Character'));file.set(layout,'closeupCharacter',ref(closeup.id));
    file.center(closeup,-4*323/181*.75,79.5*323/181*.75,417*323/181*.75,507*323/181*.75);
    file.center(file.get(file.id(layout,'arena')),210*.75,14*.75,310,310,464/310*.75);
    const hint=file.go(file.get(file.id(layout,'startHint'))),hintText=file.component(hint,textGuid);file.set(hintText,'m_text',JSON.stringify('W를 눌러 시작 · W → D → S → A'));file.set(hintText,'m_fontSize',15);file.set(hintText,'m_fontSizeBase',15);
    const hud=file.script('StirHudView');file.set(hud,'startHint',ref(hint.id));file.set(file.get(file.id(hud,'startOverlay')),'m_IsActive',0);
    const glass=file.script('StirGlassView');const timer=file.get(file.id(glass,'timerRing'));file.set(timer,'m_Sprite',sprite('03.Crafting/02.Minigames/01.Stir/GAU_stir_round_A.png'));file.center(file.rect(file.go(timer)),0,0,416*310/464,416*310/464);
    const base=file.find('Timer Ring Base');file.set(file.component(base,imageGuid),'m_Sprite',sprite('03.Crafting/02.Minigames/01.Stir/GAU_stir_round_B.png'));file.center(file.rect(base),0,0,416*310/464,416*310/464);
    const closeupFrame=file.get(file.id(layout,'closeupFrame'));
    const frame=file.component(file.go(closeupFrame),imageGuid);file.set(frame,'m_Sprite',sprite('03.Crafting/02.Minigames/01.Stir/WebGenerated/Web_GRD_mix_detail_stir_detail_background.png'));file.set(frame,'m_Type',0);file.set(frame,'m_PreserveAspect',0);
    const border=file.image('Stir Closeup Border',closeupFrame,frame,sprite('03.Crafting/02.Minigames/01.Stir/WebGenerated/Web_GRD_mix_detail_stir_detail_border.png'),'ffffffff',0,0,187,325);file.stretch(border.rect);
    const closeupParent=file.get(file.id(closeupFrame,'m_Father'));
    const siblings=file.children(closeupParent).filter(id=>id!==closeupFrame.id);
    const viewportIndex=siblings.indexOf(file.id(layout,'closeupViewport'));
    assert(viewportIndex>=0,'Closeup frame and viewport must share their drawing parent.');
    siblings.splice(viewportIndex,0,closeupFrame.id);file.setChildren(closeupParent,siblings);
    for(const badge of file.blocks.filter(b=>b.text.includes('m_EditorClassIdentifier: Assembly-CSharp::StirKeyBadge\n'))){for(const [key,suffix] of [['idleSprite','C'],['currentSprite','A'],['nextSprite','B']])file.set(badge,key,sprite(`03.Crafting/02.Minigames/01.Stir/BTN_key_${suffix}.png`));file.set(badge,'artworkSize',vector(61*310/464,61*310/464));}
    for(const [key,x,y] of [['W',0,155],['D',155,0],['S',0,-155],['A',-155,0]])file.center(file.rect(file.find('Key '+key)),x,y,61*310/464,61*310/464);
    file.center(file.rect(file.find('Top View Glass')),0,0,348.75*310/464,360*310/464);
}

function shake(file) {
    const board=file.rect(file.find('Rhythm Board'));file.center(board,248.25,7.875,430.5,441.75);
    const im=file.component(file.go(board),imageGuid);assert(im);file.set(im,'m_Sprite',sprite('03.Crafting/02.Minigames/04.Shake/WebGenerated/Web_GRD_mix_board_rhythm_board_background.png'));file.set(im,'m_Color',color('ffffffff'));file.set(im,'m_Type',0);
    const oldChildren=file.children(board);
    const content=file.node('Rhythm Reference Content',board);file.center(content.rect,0,0,390,517,.83);
    for(const id of oldChildren){if(id===content.rect.id)continue;const go=file.go(file.get(id));if(!file.name(go).startsWith('Rhythm Artwork '))file.parent(file.get(id),content.rect);}
    for(const [name,asset] of [['Dots','before'],['Scanlines','after']]){const layer=file.image('Rhythm Artwork '+name,board,im,sprite(`03.Crafting/02.Minigames/04.Shake/WebGenerated/Web_GRD_mix_board_rhythm_board_${asset}_background.png`),'ffffffff',0,0,430.5,441.75);file.stretch(layer.rect);}
    const layers=file.children(board).filter(id=>id!==content.rect.id);file.setChildren(board,[...layers,content.rect.id]);
}

const targets = [
    ['Assets/01.Game/01.Scenes/Play.unity',commonHud],
    ['Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/02.Stir/NewStirGame.prefab',stir],
    ['Assets/02.Dev/01.Scenes/01.Minigames/Stir.unity',stir],
    ['Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/01.Shake/RhythmShakingMinigame.prefab',shake],
];
const apply=process.argv.includes('--apply');let changed=0;
for(const [relative,migrate] of targets){const file=new UnityFile(relative);migrate(file);file.validate();const output=file.serialize();if(output!==file.original){changed++;if(apply)fs.writeFileSync(file.file,output);else throw new Error(`UI is not aligned: ${relative}`);}console.log((apply?'ALIGNED ':'CHECKED ')+relative);}
console.log(`GIMMICK_UI_OK: ${targets.length} serialized assets, ${usedSprites.size} source sprites, ${changed} changes; hierarchy and bindings verified.`);
