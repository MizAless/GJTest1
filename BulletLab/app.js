(() => {
  'use strict';
  const {Lab,PROPS,TRIGGERS,slot,tags,clone,fmt}=BulletLab;
  const lab=new Lab();let selected=0,editing='bullet',busy=false,fast=false,view=null;
  const $=id=>document.getElementById(id);
  const wait=ms=>new Promise(resolve=>setTimeout(resolve,fast?12:ms));
  const COLORS={burn:'#f18b57',poison:'#9fcf7b',chill:'#85cefa',bleed:'#f58c99',puncture:'#c8a2e5'};
  const ICONS={ricochet:'↗',pierce:'↓',fan:'⋔',burst:'≡',split:'Y',burn:'♨',chill:'❄',bleed:'滴',poison:'☠',puncture:'†',overload:'✹'};
  const make=(props={},trigger='none')=>({props,trigger});
  const PRESETS={
    'Чистый барабан':()=>Array.from({length:6},slot),
    'Веер / очередь':()=>[...['fan','burst','ricochet','pierce','split'].map(k=>({bullet:make({[k]:2}),module:make()})),slot()],
    'Поджог → перегрузка':()=>[...Array.from({length:2},()=>({bullet:make({burn:1}),module:make()})),{bullet:make({burn:1,overload:1}),module:make()},...Array.from({length:3},slot)],
    'Проколы':()=>Array.from({length:6},(_,i)=>({bullet:make(i===2?{overload:1}:{puncture:1}),module:make()})),
    'Передача свойств':()=>[{bullet:make({ricochet:1},'reloadNext'),module:make({burn:1},'reloadTwo')},{bullet:make({more:50},'reloadNext'),module:make({},'reloadRepeat')},{bullet:make({overload:1}),module:make({burst:1},'shotNext')},{bullet:make({poison:1}),module:make({},'shotRepeatNext')},{bullet:make({split:1}),module:make()},{bullet:make({puncture:1}),module:make({},'shotPast')}]
  };
  function log(text,kind='note'){const el=document.createElement('div');el.className='entry '+kind;el.textContent=text;$('log').prepend(el);while($('log').children.length>160)$('log').lastChild.remove();}
  function summary(p){return Object.entries(p).filter(([,n])=>n).map(([k,n])=>`${PROPS[k].name} ${PROPS[k].unit==='%'?'+'+n+'%':PROPS[k].unit==='+'?'+'+n:'✓'}`).join(' · ')||'Обычный';}
  function renderSlots(){const profiles=view?view.profiles:lab.slots.map((s,i)=>lab.getProfile(i));$('slots').innerHTML=lab.slots.map((s,i)=>{const pr=profiles[i],keys=Object.keys(pr.p).filter(k=>pr.p[k]),dominant=keys.find(k=>COLORS[k]),name=keys.length?PROPS[keys[0]].name:'Обычный';return `<div class="slot ${selected===i?'selected':''}" data-index="${i}" draggable="${!busy}" role="button" tabindex="0" aria-label="Гнездо ${i+1}"><small class="index">0${i+1}</small><div class="bullet-icon" style="--bullet:${COLORS[dominant]||'#bdcedb'}"><b>${ICONS[keys[0]]||'•'}</b></div><strong>${name}</strong><div class="stats"><span class="chip ${pr.damage!==10?'buff':''}">⚔ ${pr.damage}</span></div><div class="tags">${tags(pr.p).join(' · ')}</div><small title="${summary(pr.p)}">${keys.filter(k=>ICONS[k]).map(k=>ICONS[k]+(pr.p[k]>1?pr.p[k]:'')).join(' ')||'Без свойств'}</small><div class="stats"><span class="chip module">${Object.keys(s.module.props).some(k=>s.module.props[k])?'Модуль ✓':'Без модуля'}</span></div></div>`;}).join('');
    document.querySelectorAll('.slot').forEach(el=>{const select=()=>{if(busy)return;selected=+el.dataset.index;renderSlots();renderEditor();};el.onclick=select;el.onkeydown=e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();select();}};el.ondragstart=e=>{if(busy){e.preventDefault();return;}e.dataTransfer.setData('text/plain',el.dataset.index);};el.ondragover=e=>e.preventDefault();el.ondrop=e=>{e.preventDefault();if(busy)return;const raw=e.dataTransfer.getData('text/plain');if(!/^[0-5]$/.test(raw))return;const from=+raw,to=+el.dataset.index; // The cartridge moves; the socket module stays in place.
      [lab.slots[from].bullet,lab.slots[to].bullet]=[lab.slots[to].bullet,lab.slots[from].bullet];selected=to;changed('Патроны переставлены; модули остались в гнёздах.');};});
  }
  function stateText(status) {
    const lines = [];
    for (const [key, title] of [['burn', 'Огонь'], ['poison', 'Яд'], ['bleed', 'Кровотечение']]) {
      const charges = status[key];
      if (charges.length === 0) {
        continue;
      }
      const damage = fmt(charges.reduce((total, charge) => total + charge.damage, 0));
      const minimumTicks = Math.min(...charges.map(charge => charge.left));
      const maximumTicks = Math.max(...charges.map(charge => charge.left));
      lines.push(`${title} ${charges.length} (−${damage}/тик; ${minimumTicks}–${maximumTicks} т.)`);
      if (key !== 'bleed') {
        lines.push(`↳ До перегрузки: ${fmt(status.overloadProgress[key])} / 5 урона`);
      }
    }
    if (status.chill) {
      lines.push(`Холод ${status.chill}`);
    }
    if (status.puncture) {
      lines.push(`Прокол ${status.puncture}`);
    }
    if (status.frozen) {
      lines.push('❄ Заморожен');
    }
    return lines.join('<br>') || 'Без состояний';
  }
  function renderTargets(){const ts=view?view.targets:lab.targets;$('targets').innerHTML=ts.map(t=>`<div class="target ${t.hp<=0?'dead':''}" data-target="${t.id}" style="grid-column:${t.x+1};grid-row:${t.y+1};outline:${t.id===lab.aim?'2px solid #f5ba66':'none'}" role="button" tabindex="0"><div class="face">${t.max===30?'♟':t.max===50?'♜':'♛'}</div><strong>${t.name} #${t.id+1}</strong><div class="health"><i style="width:${100*t.hp/t.max}%"></i></div><small>${t.hp} / ${t.max} HP</small><div class="state-list">${stateText(t.status)}</div></div>`).join('');document.querySelectorAll('.target').forEach(el=>{const select=()=>{if(busy||!lab.alive(+el.dataset.target))return;lab.aim=+el.dataset.target;renderTargets();};el.onclick=select;el.onkeydown=e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();select();}};});}
  function renderPhase(){ $('phase').textContent=busy?'Срабатывает цепочка…':lab.dirty?'Раскладка изменена. Перезаряди барабан.':lab.index>=6?'Барабан пуст. Полная перезарядка не вызовет тик.':`Следующий выстрел: ${lab.index+1} / 6 · один тик после завершения выстрела.`;}
  function triggerHelp(key){if(key==='none')return 'Свойства всегда действуют на свой патрон.';if(key==='reloadRepeat')return 'Повторяет триггеры перезарядки обоих слоёв предыдущего гнезда. Повтор не вызывает новый повтор.';if(key==='shotRepeatNext')return 'После своего выстрела запускает следующий патрон целиком, без расхода его гнезда, без его триггеров и без отдельного тика.';if(key==='shotPast')return 'После своего выстрела наносит одно попадание текущими свойствами по каждой живой цели предыдущего выстрела. Веер и очередь здесь не запускаются.';return 'Собственные свойства сохраняются; этот слой дополнительно копирует их в указанные гнёзда. Бонусы выстрела сохраняются до полной перезарядки. Порядок круговой: после 6 идёт 1.';}
  function renderEditor(){const l=lab.slots[selected][editing];$('slotNum').textContent=selected+1;$('editBullet').classList.toggle('chosen',editing==='bullet');$('editModule').classList.toggle('chosen',editing==='module');$('triggerSelect').value=l.trigger;$('triggerDescription').textContent=triggerHelp(l.trigger);
    let group='';$('propertyEditor').innerHTML=Object.entries(PROPS).map(([k,p])=>{const g=['ricochet','pierce','fan','burst','split'].includes(k)?'Снаряды':['burn','chill','bleed','poison','puncture','overload'].includes(k)?'Состояния':'Множители';const title=group!==g?`<div class="group-title">${g}</div>`:'';group=g;return `${title}<div class="property-row ${(l.props[k]||0)?'on':''}"><label for="prop-${k}" title="${p.desc}">${p.name} ${p.unit}<small>${p.desc}</small></label><input id="prop-${k}" data-property="${k}" type="number" min="0" max="${p.max}" step="${p.step||1}" value="${l.props[k]||0}" ${busy?'disabled':''}></div>`;}).join('');
    document.querySelectorAll('[data-property]').forEach(el=>el.onchange=()=>{if(busy)return;const key=el.dataset.property,value=Math.max(0,Math.min(PROPS[key].max,Math.round(+el.value||0)));if(value)l.props[key]=value;else delete l.props[key];changed();});
    const pr=lab.getProfile(selected);$('inspectStats').textContent=`Урон попадания: 10 × ${fmt(pr.factors.more)} × ${fmt(pr.factors.projectileMore)} = ${pr.damage}. Множители: постепенный ×${fmt(pr.factors.more*pr.factors.dotMore)}, область ×${fmt(pr.factors.more*pr.factors.areaMore)}, по цели с состоянием дополнительно ×${fmt(pr.factors.afflictedMore)}. ${summary(pr.p)}. Теги: ${tags(pr.p).join(', ')}.`;
  }
  function render(){renderSlots();renderTargets();renderPhase();renderEditor();const ts=view?view.targets:lab.targets;$('totals').textContent=`Суммарно нанесено: ${fmt(ts.reduce((n,t)=>n+t.max-t.hp,0))} урона. Врагов осталось: ${ts.filter(t=>t.hp>0).length}.`;}
  function changed(message){lab.edit();view=null;if(message)log(message);render();}
  function setBusy(v){busy=v;for(const id of ['reload','fire','step','resetDrum','resetTargets','triggerSelect','targetMode','editBullet','editModule'])$(id).disabled=v;document.querySelectorAll('#presets button,[data-property]').forEach(el=>el.disabled=v);renderPhase();}
  async function beam(a,b){if(!a||!b)return;const x=a.getBoundingClientRect(),y=b.getBoundingClientRect(),dx=y.left+y.width/2-x.left-x.width/2,dy=y.top+y.height/2-x.top-x.height/2;const el=document.createElement('div');el.className='beam';el.style.cssText=`left:${x.left+x.width/2}px;top:${x.top+x.height/2}px;width:${Math.hypot(dx,dy)}px;transform:rotate(${Math.atan2(dy,dx)}rad)`;document.body.append(el);await wait(350);el.remove();}
  async function play(events){for(const e of events){view=e;renderSlots();renderTargets();log(e.text,e.kind);const origin=e.from??e.source;const a=origin===undefined||origin===null?null:document.querySelector(`.slot[data-index="${origin}"]`);const b=e.to!==undefined?document.querySelector(`.slot[data-index="${e.to}"]`):e.target!==undefined?document.querySelector(`.target[data-target="${e.target}"]`):null;if(a)a.classList.add('active');if(b)b.classList.add('targeted');if(a&&b)await beam(a,b);if(e.amount&&b&&!fast){const r=b.getBoundingClientRect(),pop=document.createElement('div');pop.className='damage-pop';pop.textContent='−'+e.amount;pop.style.left=(r.left+r.width/2)+'px';pop.style.top=(r.top+20)+'px';document.body.append(pop);setTimeout(()=>pop.remove(),750);}await wait(e.amount?150:80);}
    view=null;render();$('totals').textContent=`Суммарно нанесено: ${fmt(lab.targets.reduce((n,t)=>n+t.max-t.hp,0))} урона. Врагов осталось: ${lab.targets.filter(t=>t.hp>0).length}.`;}
  async function action(kind){if(busy)return;setBusy(true);try{if(kind==='all'){while(!lab.dirty&&lab.index<6&&lab.choose()>=0)await play(lab.fire());if(lab.dirty||lab.index>=6)log(lab.dirty?'Сначала перезаряди барабан.':'Барабан пуст.');}else await play(kind==='reload'?lab.reload():lab.fire());}finally{setBusy(false);render();}}
  $('triggerSelect').innerHTML=Object.entries(TRIGGERS).map(([k,v])=>`<option value="${k}">${v}</option>`).join('');$('triggerSelect').onchange=e=>{lab.slots[selected][editing].trigger=e.target.value;changed();};
  $('editBullet').onclick=()=>{editing='bullet';renderEditor();};$('editModule').onclick=()=>{editing='module';renderEditor();};
  $('presets').innerHTML=Object.keys(PRESETS).map(name=>`<button class="btn" data-preset="${name}">${name}</button>`).join('');
  function loadPreset(name){if(busy)return;lab.slots=clone(PRESETS[name]());lab.edit();lab.resetTargets($('targetMode').value);view=null;selected=0;$('log').innerHTML='';log(`Раскладка «${name}». Настрой свойства и перезаряди барабан.`);render();}
  $('presets').onclick=e=>{if(e.target.dataset.preset)loadPreset(e.target.dataset.preset);};$('resetDrum').onclick=()=>loadPreset('Чистый барабан');
  $('resetTargets').onclick=()=>{if(busy)return;lab.resetTargets($('targetMode').value);view=null;log('Враги восстановлены, состояния и история попаданий сброшены.');render();};$('targetMode').onchange=$('resetTargets').onclick;
  $('reload').onclick=()=>action('reload');$('step').onclick=()=>action('step');$('fire').onclick=()=>action('all');$('instant').onclick=()=>{fast=!fast;$('instant').textContent=`Анимация: ${fast?'быстрая':'обычная'}`;};
  $('rulesText').innerHTML='<p><b>Общее:</b> свойства патрона и модуля действуют на свой патрон. Их триггеры могут передавать те же свойства соседям. Перестановка перемещает патрон, а модуль остаётся в гнезде. Полная перезарядка заново рассчитывает бонусы.</p><p><b>Множители:</b> два бонуса +50% дают 10 × 1,5 × 1,5 = 22,5, а не 20. «Весь урон» влияет на все виды урона; остальные множители — на свой тип. Урон сохраняется с точностью до сотых.</p><p><b>Состояния:</b> поджог — 20% базового урона за тик × 3 тика; яд — 15% × 4; кровотечение — 25% × 2. Каждый заряд хранит силу при наложении. Тик после каждого выстрела, в том числе шестого; не после каждого снаряда. Холод и прокол сохраняются до перегрузки или сброса врагов.</p><p><b>Перегрузка:</b> огонь и яд отдельно копят нанесённый ими урон. При 5 урона загорается естественный взрыв: огонь — 15 по области, яд — 10 и разнос одного заряда яда соседям. Оставшиеся заряды сохраняются, счётчик сбрасывается; максимум один взрыв каждого типа за тик, избыток не переносится. При окончании состояния счётчик обнуляется. Попадание с «Перегрузкой» взрывает даже один заряд сразу и снимает только соответствующее состояние. Взрыв имеет полную силу: естественный наследует множители самого сильного тикающего заряда, досрочный — текущего патрона. Новый разнесённый при тике яд тикает со следующего выстрела. Взрывы сами не заряжают счётчики. Холод и прокол по-прежнему требуют свойства перегрузки: 3 холода → заморозка, следующий удар ×1,5 и раскол 15 по области; 2 прокола → 40 урона одной цели. Кровотечение пока без перегрузки. Числа тестовые.</p><p><b>Снаряды:</b> +n очереди означает 1+n выпусков; +n веера — 1+n снарядов на выпуск. Пробитие идёт за цель в глубину колонки, затем рикошет выбирает ближайшую другую цель. Разветвлённые копии наследуют свойства и историю своей ветви. Одна ветвь не поражает одну цель дважды. Предел — 160 попаданий на запуск, остановка явно отражается в журнале.</p>';
  loadPreset('Чистый барабан');
})();

