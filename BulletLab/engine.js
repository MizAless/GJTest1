/* Deterministic sandbox rules. No Unity integration. */
(function(root) {
  'use strict';
  const PROPS = {
    ricochet:{name:'Рикошет',tag:'снаряды',max:5,unit:'+',desc:'Дополнительные переходы к ближайшей другой цели.'},
    pierce:{name:'Пробитие',tag:'снаряды',max:5,unit:'+',desc:'Дополнительные цели позади первой в той же колонке.'},
    fan:{name:'Веер',tag:'снаряды',max:5,unit:'+',desc:'Дополнительные снаряды одновременно по разным целям. Лишние пролетают мимо, без попадания в одну цель несколько раз.'},
    burst:{name:'Очередь',tag:'снаряды',max:5,unit:'+',desc:'Дополнительные последовательные выпуски по выбранной цели. Вся очередь расходует один патрон и вызывает один тик.'},
    split:{name:'Разветвление',tag:'снаряды',max:3,unit:'+',desc:'После попадания создаёт такие же снаряды к другим целям. Наследует свойства; одна ветвь не возвращается к уже задетым ею целям.'},
    burn:{name:'Поджог',tag:'огонь',max:5,unit:'+',desc:'Заряд: 20% урона за тик, 3 тика. Каждые 5 накопленного урона огня зажигают взрыв, сохраняя оставшиеся заряды; максимум один взрыв за тик.'},
    chill:{name:'Охлаждение',tag:'холод',max:5,unit:'+',desc:'Накапливает холод. Перегрузка при 3 зарядах замораживает; следующее попадание раскалывает цель.'},
    bleed:{name:'Кровотечение',tag:'кровотечение',max:5,unit:'+',desc:'Заряд: 25% урона патрона за тик, 2 тика.'},
    poison:{name:'Яд',tag:'яд',max:5,unit:'+',desc:'Заряд: 15% урона за тик, 4 тика. При 5 накопленного урона яда — взрыв и разнос яда; оставшиеся заряды сохраняются. Новый яд тикает со следующего выстрела.'},
    puncture:{name:'Прокол',tag:'прокол',max:5,unit:'+',desc:'Добавляет проколы. После попадания наносит отдельно 2 × число проколов² урона.'},
    overload:{name:'Перегрузка',tag:'область',max:1,unit:'',desc:'Досрочно взрывает любой активный огонь и яд, снимая только сработавшее состояние и его счётчик. Сила взрыва полная. Холод: от 3 зарядов; прокол: от 2. Кровотечение не перегружается.'},
    more:{name:'Весь урон',tag:'урон',max:200,step:25,unit:'%',desc:'Отдельный множитель всего урона: +50% означает ×1,5.'},
    projectileMore:{name:'Урон снарядов',tag:'снаряды',max:200,step:25,unit:'%',desc:'Множитель прямых попаданий; не усиливает постепенный урон.'},
    dotMore:{name:'Постепенный урон',tag:'постепенный урон',max:200,step:25,unit:'%',desc:'Множитель поджога, яда и кровотечения.'},
    areaMore:{name:'Урон области',tag:'область',max:200,step:25,unit:'%',desc:'Множитель взрывов перегрузки и раскола.'},
    afflictedMore:{name:'По цели с состоянием',tag:'урон',max:200,step:25,unit:'%',desc:'Множитель попадания по врагу, у которого уже есть любое состояние.'}
  };
  const TRIGGERS = {
    none:'Без триггера',reloadNext:'Перезарядка → следующий',reloadPrev:'Перезарядка → предыдущий',reloadTwo:'Перезарядка → два следующих',reloadAll:'Перезарядка → все остальные',reloadRepeat:'Перезарядка → повтор предыдущего',
    shotNext:'Выстрел → усилить следующий',shotPrev:'Выстрел → усилить предыдущий',shotTwo:'Выстрел → усилить два следующих',shotRepeatNext:'Выстрел → запустить следующий',shotPast:'Выстрел → прошлые попадания'
  };
  const layer=()=>({props:{},trigger:'none'});
  const slot=()=>({bullet:layer(),module:layer()});
  const clone=v=>JSON.parse(JSON.stringify(v));
  const wrap=n=>(n+6)%6;
  const fmt=n=>Math.round(n*100)/100;
  const NATURAL_OVERLOAD_THRESHOLD = 5;
  const multiplier=(props,key)=>1+(props[key]||0)/100;
  function profile(layers){const p={};let factors={more:1,projectileMore:1,dotMore:1,areaMore:1,afflictedMore:1};
    for(const l of layers)for(const [key,value] of Object.entries(l.props)){if(key in factors)factors[key]*=1+value/100;else p[key]=(p[key]||0)+value;}
    return {p,factors,damage:fmt(10*factors.more*factors.projectileMore)};
  }
  function tags(p){const result=new Set(['снаряды']);for(const [key,n] of Object.entries(p))if(n){result.add(PROPS[key].tag);if(['burn','poison','bleed'].includes(key))result.add('постепенный урон');}return [...result];}
  class Lab {
    constructor(){this.slots=Array.from({length:6},slot);this.resetTargets('group');this.dirty=true;this.index=0;this.layers=[];this.events=[];}
    resetTargets(mode='group'){this.mode=mode;const defs=mode==='solo'?[[80,1,0]]:mode==='trio'?[[30,0,0],[50,1,0],[80,2,0]]:[[30,0,0],[50,1,0],[80,2,0],[30,0,1],[50,1,1],[80,2,1]];
      this.targets=defs.map(([hp,x,y],id)=>({id,name:hp===30?'Хилый':hp===50?'Средний':'Большой',max:hp,hp,x,y,status:{burn:[],poison:[],bleed:[],chill:0,puncture:0,frozen:false,overloadProgress:{burn:0,poison:0}}}));this.aim=0;this.past=[];this.index=0;}
    edit(){this.dirty=true;this.layers=[];this.index=0;this.past=[];}
    getProfile(i){return profile(this.layers[i]||[this.slots[i].bullet,this.slots[i].module]);}
    emit(text,kind='note',extra={}){this.events.push({text,kind,...extra,targets:clone(this.targets),profiles:this.slots.map((s,i)=>this.getProfile(i))});}
    destinations(i,trigger){if(trigger.endsWith('Next'))return[wrap(i+1)];if(trigger.endsWith('Prev'))return[wrap(i-1)];if(trigger.endsWith('Two'))return[wrap(i+1),wrap(i+2)];if(trigger.endsWith('All'))return Array.from({length:5},(_,k)=>wrap(i+k+1));return[];}
    grant(from,to,l){this.layers[to].push(clone(l));const payload=Object.entries(l.props).filter(([,n])=>n).map(([k,n])=>`${PROPS[k].name} ${PROPS[k].unit==='%'?'+'+n+'%':'+'+n}`).join(', ')||'нет свойств для передачи';this.emit(`${from+1} → ${to+1}: ${payload}. Урон ${this.getProfile(to).damage}.`,'reload',{from,to});}
    reload(){this.events=[];this.layers=this.slots.map(s=>[clone(s.bullet),clone(s.module)]);this.index=0;this.dirty=false;
      this.emit('Перезарядка: базовые свойства восстановлены. Состояния врагов сохраняются, тика нет.','reload');
      const activate=(i,l)=>{for(const j of this.destinations(i,l.trigger))this.grant(i,j,l);};
      for(let i=0;i<6;i++)for(const l of [this.slots[i].bullet,this.slots[i].module]){
        if(l.trigger==='reloadRepeat'){const prev=wrap(i-1);this.emit(`${i+1} → ${prev+1}: повтор триггеров перезарядки.`,'reload',{from:i,to:prev});for(const src of [this.slots[prev].bullet,this.slots[prev].module])if(src.trigger.startsWith('reload')&&src.trigger!=='reloadRepeat')activate(prev,src);}
        else if(l.trigger.startsWith('reload'))activate(i,l);
      }this.emit('Барабан готов. Бонусы не накапливаются между перезарядками.','reload');return this.events;}
    alive(id){return id>=0&&this.targets[id]&&this.targets[id].hp>0;}
    choose(){return this.alive(this.aim)?this.aim:this.targets.findIndex(t=>t.hp>0);}
    afflicted(t){return t.status.chill>0||t.status.puncture>0||t.status.frozen||['burn','poison','bleed'].some(k=>t.status[k].length);}
    hurt(id,n,label,source,kind='shot'){if(!this.alive(id))return;n=fmt(n);this.targets[id].hp=fmt(Math.max(0,this.targets[id].hp-n));this.emit(`${this.targets[id].name} #${id+1}: −${n} HP · ${label}`,kind,{source,target:id,amount:n});}
    neighbors(id){const t=this.targets[id];return this.targets.filter(o=>o.id!==id&&o.hp>0&&Math.abs(t.x-o.x)+Math.abs(t.y-o.y)<=1).map(o=>o.id);}
    nearest(id,visited){const t=this.targets[id];return this.targets.filter(o=>o.hp>0&&!visited.has(o.id)).sort((a,b)=>(Math.abs(a.x-t.x)+Math.abs(a.y-t.y))-(Math.abs(b.x-t.x)+Math.abs(b.y-t.y))||a.id-b.id).map(o=>o.id);}
    area(id,n,label,source){for(const j of [id,...this.neighbors(id)])this.hurt(j,n,label,source,'echo');}
    hit(id,pr,source,record=true){if(!this.alive(id))return;const t=this.targets[id],s=t.status,p=pr.p,f=pr.factors;let hitDamage=10*f.more*f.projectileMore*(this.afflicted(t)?f.afflictedMore:1);
      const frozen=s.frozen;if(frozen){s.frozen=false;hitDamage*=1.5;}
      this.hurt(id,hitDamage,'попадание',source);if(record)this.currentHits.add(id);
      // Shatter happens even when the triggering direct hit kills the frozen enemy.
      if(frozen)this.area(id,15*f.more*f.areaMore,'раскол',source);
      if(!this.alive(id))return;
      for (const key of ['burn', 'poison', 'bleed']) {
        const coefficient = {burn: 0.2, poison: 0.15, bleed: 0.25}[key];
        const ticks = {burn: 3, poison: 4, bleed: 2}[key];
        for (let count = 0; count < (p[key] || 0); count++) {
          s[key].push({
            damage: fmt(10 * f.more * f.dotMore * coefficient),
            left: ticks,
            overloadDamage: (key === 'burn' ? 15 : 10) * f.more * f.areaMore,
            spreadDamage: fmt(1.5 * f.more * f.dotMore)
          });
        }
      }
      s.chill+=p.chill||0;s.puncture+=p.puncture||0;
      if(p.burn||p.poison||p.bleed||p.chill||p.puncture)this.emit(`Состояния наложены на #${id+1}.`,'echo',{source,target:id});
      if(s.puncture)this.hurt(id,2*s.puncture*s.puncture*f.more,'прокол: 2 × заряды²',source,'echo');
      if(!this.alive(id)||!p.overload)return;
      if (s.burn.length > 0) {
        this.overloadDot(id, 'burn', 15*f.more*f.areaMore, 1.5*f.more*f.dotMore, source, true);
      }
      if(!this.alive(id))return;
      if(s.chill>=3){s.chill=0;s.frozen=true;this.emit(`#${id+1}: заморозка. Следующий удар ×1,5 и раскол по области.`,'echo',{source,target:id});}
      if (s.poison.length > 0) {
        this.overloadDot(id, 'poison', 10*f.more*f.areaMore, 1.5*f.more*f.dotMore, source, true);
      }
      if(this.alive(id)&&s.puncture>=2){s.puncture=0;this.hurt(id,40*f.more,'перегрузка прокола',source,'echo');}
    }
    projectiles(source,aim,triggered=false){const pr=this.getProfile(source),p=pr.p;const visitedBudget={count:0,limited:false};
      const travel=(id,visited,pierces,bounces)=>{if(!this.alive(id)||visited.has(id))return;if(++visitedBudget.count>160){visitedBudget.limited=true;return;}const path=new Set(visited);path.add(id);this.hit(id,pr,source);
        const children=this.nearest(id,path).slice(0,p.split||0);for(const child of children)travel(child,new Set(path),p.pierce||0,p.ricochet||0);
        const here=this.targets[id];if(pierces>0){const next=this.targets.find(t=>t.hp>0&&t.x===here.x&&t.y>here.y&&!path.has(t.id));if(next){travel(next.id,path,pierces-1,bounces);return;}}
        if(bounces>0){const next=this.nearest(id,path)[0];if(next!==undefined)travel(next,path,0,bounces-1);}
      };
      for(let round=0;round<1+(p.burst||0);round++){const target=this.alive(aim)?aim:this.choose();if(target<0)break;this.emit(`${triggered?'Вызванный патрон':'Патрон'} ${source+1}: выпуск ${round+1}/${1+(p.burst||0)}, веер ${1+(p.fan||0)}.`,'shot',{source});const spread=[target,...this.nearest(target,new Set([target]))];for(let n=0;n<1+(p.fan||0);n++){if(spread[n]!==undefined)travel(spread[n],new Set(),p.pierce||0,p.ricochet||0);else this.emit('Лишний снаряд веера: нет отдельной цели.','note');}}
      if(visitedBudget.limited)this.emit('Достигнут предел 160 попаданий на один запуск патрона; остальные ветви остановлены.','note');
    }
    overloadDot(id, key, damage, spreadDamage, source, consumeStatus) {
      const status = this.targets[id].status;
      status.overloadProgress[key] = 0;
      if (consumeStatus) {
        status[key] = [];
      }

      const neighbors = key === 'poison' ? this.neighbors(id) : [];
      const name = key === 'burn' ? 'огненная перегрузка' : 'ядовитая перегрузка';
      const mode = consumeStatus ? 'досрочная, состояние снято' : 'естественная, заряды сохраняются';
      this.area(id, damage, name + ' · ' + mode, source);
      for (const neighbor of neighbors) {
        if (this.alive(neighbor)) {
          this.targets[neighbor].status.poison.push({
            damage: fmt(spreadDamage), left: 4, overloadDamage: damage, spreadDamage: fmt(spreadDamage)
          });
        }
      }
    }

    tick() {
      this.emit('Смена патрона: один тик всех постепенных состояний.', 'tick');
      // Snapshot every target before spreading poison, independent of target traversal order.
      const snapshots = this.targets.map(target => ({
        burn: [...target.status.burn],
        poison: [...target.status.poison],
        bleed: [...target.status.bleed]
      }));

      for (const target of this.targets) {
        if (!this.alive(target.id)) continue;
        for (const key of ['burn', 'poison', 'bleed']) {
          const charges = snapshots[target.id][key];
          const damage = charges.reduce((sum, charge) => sum + charge.damage, 0);
          for (const charge of charges) charge.left--;
          target.status[key] = target.status[key].filter(charge => charge.left > 0);
          const previousHealth = target.hp;
          if (damage > 0) {
            this.hurt(target.id, damage, {burn:'поджог', poison:'яд', bleed:'кровотечение'}[key], null, 'tick');
          }
          if (!this.alive(target.id)) break;

          if (key === 'burn' || key === 'poison') {
            let progress = fmt(target.status.overloadProgress[key] + previousHealth - target.hp);
            if (progress >= NATURAL_OVERLOAD_THRESHOLD && charges.length > 0) {
              const strongest = charges.reduce((best, charge) => charge.overloadDamage > best.overloadDamage ? charge : best);
              this.overloadDot(target.id, key, strongest.overloadDamage, strongest.spreadDamage, null, false);
              progress = 0;
            }
            if (target.status[key].length === 0) progress = 0;
            target.status.overloadProgress[key] = progress;
          }
          if (!this.alive(target.id)) break;
        }
      }
    }
    fire(){this.events=[];if(this.dirty){this.emit('После изменения сборки сначала перезарядите барабан.');return this.events;}if(this.index>=6){this.emit('Барабан пуст. Нужна полная перезарядка.');return this.events;}const target=this.choose();if(target<0){this.emit('Все враги уничтожены. Сбросьте мишени.');return this.events;}
      const i=this.index++;this.currentHits=new Set();const previous=[...this.past];this.emit(`Выстрел ${i+1}.`,'shot',{source:i});
      for(const l of [this.slots[i].bullet,this.slots[i].module])if(['shotNext','shotPrev','shotTwo'].includes(l.trigger))for(const j of this.destinations(i,l.trigger))this.grant(i,j,l);
      this.projectiles(i,target);
      for(const l of [this.slots[i].bullet,this.slots[i].module]){
        if(l.trigger==='shotRepeatNext'){const next=wrap(i+1),aim=this.alive(target)?target:this.choose();if(aim>=0){this.emit(`${i+1} → ${next+1}: запуск патрона без расхода гнезда и без его триггеров.`,'echo',{from:i,to:next});this.projectiles(next,aim,true);}}
        if(l.trigger==='shotPast'){for(const id of previous)if(this.alive(id)){this.emit(`${i+1} → прошлая цель #${id+1}: одно повторное попадание свойствами текущего патрона.`,'echo',{source:i,target:id});this.hit(id,this.getProfile(i),i);}}
      }
      this.past=[...this.currentHits];this.tick();this.emit(`Выстрел завершён. Осталось ${6-this.index} патронов.`);return this.events;
    }
  }
  const api={Lab,PROPS,TRIGGERS,slot,layer,profile,tags,clone,fmt,multiplier};
  if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.BulletLab=api;
})(typeof globalThis!=='undefined'?globalThis:this);
