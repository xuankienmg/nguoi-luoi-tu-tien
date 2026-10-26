// Uses a disposable in-memory PostgreSQL WASM database, never the live Supabase project.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {PGlite}=require(path.resolve('build-check/market-sql/node_modules/@electric-sql/pglite'));
(async()=>{
 const db=await PGlite.create();let checks=0;
 await db.exec(`create role anon;create role authenticated;
 create table public.leaderboard(player_id uuid primary key,flagged boolean not null default false);
 create function public.require_version() returns void language plpgsql as $$begin return;end$$;`);
 const base=fs.readFileSync('docs/market/supabase.sql','utf8').split('-- Single-buyer migration')[0];
 const migration=fs.readFileSync('supabase/migrations/20261024_market_single_buyer.sql','utf8');
 await db.exec(base);await db.exec(migration);await db.exec(migration);checks++;
 const seller='11111111-1111-1111-1111-111111111111',a='22222222-2222-2222-2222-222222222222',b='33333333-3333-3333-3333-333333333333',secret='x'.repeat(32);
 for(const id of [seller,a,b])await db.query('select public.market_auth($1,$2,$3)',[id,secret,id===seller?'Seller':'Buyer']);
 async function listing(expired=false){return (await db.query(`insert into public.market_listings(seller_id,seller_name,item_id,qty,price,expires_at) values($1,'Seller','tu_khi_dan',1,100,clock_timestamp()+case when $2 then interval '-1 day' else interval '1 day' end) returning id`,[seller,expired])).rows[0].id;}
 const buy=(id,who=a,legacy=false)=>db.query(`select * from public.${legacy?'market_buy':'market_buy_v2'}($1,$2,'Buyer',1,$3)`,[who,secret,id]);
 async function reject(fn){let failed=false;try{await fn();}catch(error){failed=true;assert.equal(error.code,'P0001');}assert.ok(failed,'unsafe operation succeeded');checks++;}
 const id=await listing(),receipt=(await buy(id)).rows;
 assert.equal(receipt.length,1);assert.equal(String(receipt[0].listing_id),String(id));assert.equal(receipt[0].buyer_id,a);assert.equal(receipt[0].item_id,'tu_khi_dan');checks++;
 await reject(()=>buy(id,b));await reject(()=>buy(id,a));await reject(()=>buy(id,b,true));
 await reject(()=>db.query("update public.market_listings set buyer_id=$1,closed_at=clock_timestamp() where id=$2",[b,id]));
 await reject(()=>db.query("update public.market_listings set status='open',buyer_id=null where id=$1",[id]));
 await reject(()=>db.query('update public.market_listings set qty=2 where id=$1',[id]));
 await reject(()=>db.query('select public.market_cancel($1,$2,$3)',[seller,secret,id]));
 const firstClaim=(await db.query('select * from public.market_claim($1,$2)',[seller,secret])).rows;
 assert.equal(firstClaim.length,1);assert.equal(Number(firstClaim[0].payout),95);checks++;
 assert.equal((await db.query('select * from public.market_claim($1,$2)',[seller,secret])).rows.length,0);checks++;
 await reject(()=>db.query('update public.market_listings set seller_claimed=false where id=$1',[id]));
 const cancelled=await listing();await db.query('select public.market_cancel($1,$2,$3)',[seller,secret,cancelled]);
 await reject(()=>buy(cancelled,b));
 const expired=await listing(true);await reject(()=>buy(expired,b));
 const own=await listing();await reject(()=>buy(own,seller));
 const legacy=await listing();assert.equal((await buy(legacy,b,true)).rows.length,1);checks++;await reject(()=>buy(legacy,a));
 const flagged=await listing();await db.query('insert into public.leaderboard(player_id,flagged) values($1,true)',[seller]);await reject(()=>buy(flagged,a));
 await db.query('delete from public.leaderboard where player_id=$1',[seller]);
 await db.query('update public.market_players set banned=true where player_id=$1',[seller]);await reject(()=>buy(flagged,a));
 await db.query('update public.market_players set banned=false where player_id=$1',[seller]);
 await reject(()=>db.query('update public.market_listings set price=1 where id=$1',[flagged]));
 const row=(await db.query('select status,buyer_id from public.market_listings where id=$1',[id])).rows[0];assert.equal(row.status,'sold');assert.equal(row.buyer_id,a);checks++;
 console.log(`PASS: ${checks} SQL checks for migration reruns, single-buyer receipts, replay, sold-state immutability, cancel/expiry/self/banned/flagged rejection, legacy clients and exactly-once seller claims.`);
 console.log('SQL uses PGlite (one exclusive connection); concurrent client requests are covered separately by Program.cs. No live database was modified.');
 await db.close();
})().catch(e=>{console.error(e);process.exitCode=1;});
