'use strict';
const {strict:assert}=require('node:assert');
const {Lab,profile}=require('./engine.js');
function setup(mode='solo'){const lab=new Lab();lab.resetTargets(mode);return lab;}
function props(lab,i,p,module=false){lab.slots[i][module?'module':'bullet'].props=p;}
// Core balance: exactly three ordinary hits kill the 30 HP enemy.
{
  const lab=setup('trio');lab.reload();assert.equal(lab.getProfile(0).damage,10);
  lab.fire();assert.equal(lab.targets[0].hp,20);lab.fire();assert.equal(lab.targets[0].hp,10);lab.fire();assert.equal(lab.targets[0].hp,0);
  assert.deepEqual(lab.targets.map(t=>t.max),[30,50,80]);
}
// Independent percentages multiply, without additive flat damage bonuses.
assert.equal(profile([{props:{more:50}},{props:{more:50}}]).damage,22.5);
// A burst applies statuses on each hit but ticks only once, after all hits.
{
  const lab=setup();props(lab,0,{burst:2,burn:1});lab.reload();const events=lab.fire();
  assert.equal(lab.targets[0].hp,29);assert.equal(lab.targets[0].status.burn.length,3);
  assert.equal(events.filter(e=>e.kind==='tick'&&e.amount===undefined).length,1);
  assert.ok(lab.targets[0].status.burn.every(c=>c.left===2));
  const before=JSON.stringify(lab.targets);lab.reload();assert.equal(JSON.stringify(lab.targets),before);
}
// Newly inflicted DoT ticks immediately; it expires after exactly its lifetime.
{
  const lab=setup();props(lab,0,{poison:1,bleed:1});lab.reload();lab.fire();assert.equal(lab.targets[0].hp,66);
  lab.fire();assert.equal(lab.targets[0].status.bleed.length,0);assert.equal(lab.targets[0].status.poison[0].left,2);
  lab.fire();lab.fire();assert.equal(lab.targets[0].status.poison.length,0);
}
// Fan spreads hits; penetration reaches the rear row of its column.
{
  const fan=setup('trio');props(fan,0,{fan:2});fan.reload();fan.fire();assert.deepEqual(fan.targets.map(t=>t.hp),[20,40,70]);
  const pierce=setup('group');props(pierce,0,{pierce:1});pierce.reload();pierce.fire();assert.equal(pierce.targets[0].hp,20);assert.equal(pierce.targets[3].hp,20);assert.equal(pierce.targets[1].hp,50);
}
// Forked projectiles retain their poison and cannot loop indefinitely.
{
  const lab=setup('group');props(lab,0,{split:1,poison:1,ricochet:1});lab.reload();const events=lab.fire();
  assert.ok(events.length<1000);assert.ok(lab.targets.filter(t=>t.status.poison.length).length>=2);
}
// Fire overload consumes charges, cold freezes then shatters, impale overload is single target.
{
  const lab=setup();props(lab,0,{burn:3,overload:1});lab.reload();lab.fire();assert.equal(lab.targets[0].hp,55);assert.equal(lab.targets[0].status.burn.length,0);
  const cold=setup();props(cold,0,{chill:3,overload:1});cold.reload();cold.fire();assert.equal(cold.targets[0].status.frozen,true);cold.fire();assert.equal(cold.targets[0].hp,40);assert.equal(cold.targets[0].status.frozen,false);
  const impale=setup('trio');impale.aim=2;props(impale,0,{puncture:2,overload:1});impale.reload();impale.fire();assert.equal(impale.targets[2].hp,22);assert.equal(impale.targets[2].status.puncture,0);assert.equal(impale.targets[1].hp,50);
}
// Poison overload propagates one charge, never a recursive explosion.
{
  const lab=setup('trio');lab.aim=1;props(lab,0,{poison:3,overload:1});lab.reload();const events=lab.fire();
  assert.equal(lab.targets[0].status.poison.length,1);assert.equal(lab.targets[2].status.poison.length,1);assert.equal(lab.targets[1].status.poison.length,0);
  assert.equal(events.filter(e=>e.text.includes('ядовитая перегрузка')).length,3);
}
// Reload bonuses rebuild from base; they do not accumulate between reloads.
{
  const lab=setup();props(lab,0,{more:50});lab.slots[0].bullet.trigger='reloadNext';lab.reload();assert.equal(lab.getProfile(1).damage,15);lab.reload();assert.equal(lab.getProfile(1).damage,15);
  props(lab,1,{more:50},true);lab.reload();assert.equal(lab.getProfile(1).damage,22.5);
}
// Triggering the next cartridge consumes neither its slot nor an extra tick.
{
  const lab=setup();lab.slots[0].module.trigger='shotRepeatNext';props(lab,1,{burn:1});lab.reload();const events=lab.fire();assert.equal(lab.index,1);assert.equal(lab.targets[0].hp,58);assert.equal(events.filter(e=>e.kind==='tick'&&e.amount===undefined).length,1);
}
// Sixth shot still ticks; reloading afterwards never ticks.
{
  const lab=setup();props(lab,5,{burn:1});lab.reload();for(let n=0;n<6;n++)lab.fire();assert.equal(lab.targets[0].hp,18);const before=JSON.stringify(lab.targets);lab.reload();assert.equal(JSON.stringify(lab.targets),before);
}
// Natural overload preserves remaining charges and can trigger on their final tick.
{
  const lab = setup();
  props(lab, 0, {burn: 1});
  lab.reload();
  lab.fire();
  assert.equal(lab.targets[0].status.overloadProgress.burn, 2);
  lab.tick();
  assert.equal(lab.targets[0].status.overloadProgress.burn, 4);
  lab.tick();
  assert.equal(lab.targets[0].hp, 49);
  assert.equal(lab.targets[0].status.overloadProgress.burn, 0);
  assert.equal(lab.targets[0].status.burn.length, 0);
}
// A forced overload needs only one charge, including when supplied by a socket.
{
  const lab = setup();
  props(lab, 0, {burn: 1, bleed: 1});
  props(lab, 0, {overload: 1}, true);
  lab.reload();
  lab.fire();
  assert.equal(lab.targets[0].hp, 52.5);
  assert.equal(lab.targets[0].status.burn.length, 0);
  assert.equal(lab.targets[0].status.bleed.length, 1);
}
// Natural overload keeps active charges; excess damage never produces extra blasts.
{
  const lab = setup();
  props(lab, 0, {burn: 6});
  lab.reload();
  lab.fire();
  assert.equal(lab.targets[0].hp, 43);
  assert.equal(lab.targets[0].status.burn.length, 6);
  assert.equal(lab.targets[0].status.overloadProgress.burn, 0);
}
// Fire and poison progress separately; bleed cannot charge either counter.
{
  const lab = setup();
  props(lab, 0, {burn: 1, poison: 1, bleed: 1});
  lab.reload();
  lab.fire();
  assert.deepEqual(lab.targets[0].status.overloadProgress, {burn: 2, poison: 1.5});
}
// Expiration clears unfinished progress; saved area multipliers survive later shots.
{
  const expired = setup();
  props(expired, 0, {burn: 1});
  expired.reload();
  expired.fire();
  expired.targets[0].status.burn[0].left = 1;
  expired.tick();
  assert.equal(expired.targets[0].status.overloadProgress.burn, 0);

  const scaled = setup();
  props(scaled, 0, {burn: 1, areaMore: 100});
  scaled.reload();
  scaled.fire();
  scaled.tick();
  scaled.tick();
  assert.equal(scaled.targets[0].hp, 34);
}
// Poison spread during a tick waits until the next tick, on either side of its source.
for (const source of [0, 2]) {
  const lab = setup('trio');
  for (const target of lab.targets) {
    target.hp = target.max = 1000;
  }
  lab.aim = source;
  props(lab, 0, {poison: 4});
  lab.reload();
  lab.fire();
  const neighbor = lab.targets[1];
  assert.equal(neighbor.hp, 990);
  assert.equal(neighbor.status.poison.length, 1);
  assert.equal(neighbor.status.poison[0].left, 4);
  assert.equal(neighbor.status.overloadProgress.poison, 0);
}
console.log('All laboratory mechanics checks passed.');
