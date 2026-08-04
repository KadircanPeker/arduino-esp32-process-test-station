using System.Net;

namespace ProcessTestApp.Application
{
    public static class DashboardHtmlBuilder
    {
        public static string GetDashboardHtml(string localIp)
        {
            return @"<!doctype html><html lang='tr'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>Process Test Mobile</title><style>
:root{--bg:#07111f;--panel:#101d2d;--line:#26364b;--text:#e8eef7;--muted:#8ea0b8;--blue:#38bdf8;--green:#22c55e;--red:#ef4444;--amber:#f59e0b}*{box-sizing:border-box}body{margin:0;background:linear-gradient(160deg,#06101e,#0b1728);color:var(--text);font-family:Segoe UI,Arial,sans-serif;min-height:100vh}.wrap{max-width:1060px;margin:auto;padding:18px}.top{display:flex;justify-content:space-between;align-items:center;margin-bottom:14px}.brand small{display:block;color:var(--blue);font-weight:800;letter-spacing:1.7px}.brand h1{font-size:21px;margin:4px 0}.online{display:flex;align-items:center;gap:8px;color:var(--muted);font-size:13px}.dot{width:10px;height:10px;border-radius:50%;background:#64748b;box-shadow:0 0 0 4px #64748b22}.dot.on{background:var(--green);box-shadow:0 0 0 4px #22c55e22}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:10px}.card{background:rgba(16,29,45,.96);border:1px solid var(--line);border-radius:14px;padding:14px;box-shadow:0 10px 30px #0003}.kpi span{font-size:11px;color:var(--muted);text-transform:uppercase}.kpi strong{display:block;font-size:27px;margin-top:5px}.main{display:grid;grid-template-columns:1.25fr .75fr;gap:12px;margin-top:12px}.section-title{font-size:12px;text-transform:uppercase;color:var(--muted);letter-spacing:1px;margin-bottom:12px}.latest{display:grid;grid-template-columns:1fr 1fr;gap:10px}.measure{background:#0b1728;border:1px solid var(--line);border-radius:12px;padding:14px}.measure label{font-size:11px;color:var(--muted)}.measure strong{display:block;font-size:23px;margin-top:5px}.result{grid-column:1/-1;padding:14px;border-radius:12px;font-size:22px;font-weight:800;text-align:center;background:#334155}.result.pass{background:#14532d;color:#86efac}.result.fail{background:#7f1d1d;color:#fecaca}.errors{display:flex;flex-direction:column;gap:8px}.err{display:flex;justify-content:space-between;background:#0b1728;border:1px solid var(--line);padding:9px 11px;border-radius:9px}.controls{margin-top:12px}.login-row{display:grid;grid-template-columns:1fr 1fr auto;gap:8px}.login-row input{background:#0b1728;border:1px solid var(--line);color:#fff;border-radius:9px;padding:11px}.btn{border:0;border-radius:9px;padding:12px 16px;color:#fff;font-weight:700;background:#2563eb;cursor:pointer;transition:all .15s ease;user-select:none;-webkit-tap-highlight-color:transparent}.btn:active{transform:scale(.95);opacity:.85}.btn:disabled{opacity:.5;cursor:not-allowed;transform:none}.btn.red{background:#dc2626}.btn.green{background:#16a34a}.btn.gray{background:#475569}.user-bar{display:none;justify-content:space-between;align-items:center;background:#0b1728;border:1px solid var(--line);padding:10px 14px;border-radius:9px;margin-bottom:10px}.user-bar.show{display:flex}.commands{display:none;grid-template-columns:1fr 1fr 1fr;gap:8px;margin-top:10px}.commands.show{display:grid}.table-card{margin-top:12px;overflow:auto}table{width:100%;border-collapse:collapse;min-width:680px}th,td{padding:10px;border-bottom:1px solid var(--line);text-align:left;font-size:12px}th{color:var(--muted)}.tag{font-weight:800}.tag.pass{color:#4ade80}.tag.fail{color:#f87171}.toast{font-size:12px;padding:10px 14px;border-radius:8px;margin-top:10px;line-height:1.4;background:#1e293b;color:var(--muted);border:1px solid var(--line)}.toast.success{background:#14532d;color:#86efac;border-color:#22c55e44}.toast.error{background:#7f1d1d;color:#fecaca;border-color:#ef444444}.toast.warn{background:#78350f;color:#fde68a;border-color:#f59e0b44}@media(max-width:760px){.grid{grid-template-columns:1fr 1fr}.main{grid-template-columns:1fr}.login-row{grid-template-columns:1fr}.commands{grid-template-columns:1fr}.brand h1{font-size:17px}.wrap{padding:12px}}
</style></head><body><div class='wrap'><div class='top'><div class='brand'><small>ARDUINO · ESP32 · C#</small><h1>Proses Test ve İzlenebilirlik İstasyonu</h1></div><div class='online'><i id='dot' class='dot'></i><span id='device'>Cihaz bekleniyor</span></div></div>
<div class='grid'><div class='card kpi'><span>Toplam Test</span><strong id='total'>0</strong></div><div class='card kpi'><span>PASS</span><strong id='pass' style='color:var(--green)'>0</strong></div><div class='card kpi'><span>FAIL</span><strong id='fail' style='color:var(--red)'>0</strong></div><div class='card kpi'><span>Yield</span><strong id='yield'>%0</strong></div></div>
<div class='main'><div class='card'><div class='section-title'>Son Ölçüm</div><div class='latest'><div class='measure'><label id='pLabel'>Birincil ölçüm</label><strong id='pValue'>—</strong></div><div class='measure'><label id='sLabel'>İkincil ölçüm</label><strong id='sValue'>—</strong></div><div id='result' class='result'>VERİ BEKLENİYOR</div></div><div class='toast' id='detail'>Seri porttan geçerli telemetri bekleniyor.</div></div>
<div class='card'><div class='section-title'>Hata Dağılımı</div><div id='errors' class='errors'><div class='err'><span>Henüz hata yok</span><b>0</b></div></div></div></div>
<div class='card controls'><div class='section-title'>Yetkili Mobil Kontrol Panel</div>
<div id='loginBox' class='login-row'><input id='username' placeholder='Yönetici kullanıcı adı'><input id='password' type='password' placeholder='Parola'><button type='button' id='btnLogin' class='btn'>Giriş Yap</button></div>
<div id='userBar' class='user-bar'><span>👤 Yetkili Oturum: <strong id='loggedInUser' style='color:var(--blue)'>—</strong></span><button type='button' id='btnLogout' class='btn gray' style='padding:6px 12px;font-size:11px'>Çıkış Yap</button></div>
<div id='commands' class='commands'><button type='button' id='btnStart' class='btn green'>Sistemi Etkinleştir</button><button type='button' id='btnEstop' class='btn red'>Acil Durdur</button><button type='button' id='btnReset' class='btn gray'>Resetle</button></div>
<div id='authMsg' class='toast'>İzleme herkese açıktır; fiziksel komutlar yalnız Administrator oturumu ile çalışır.</div></div>
<div class='card table-card'><div class='section-title'>Son Test Kayıtları</div><table><thead><tr><th>Zaman</th><th>Seri No</th><th>Test</th><th>Birincil</th><th>İkincil</th><th>Sonuç</th><th>Hata</th></tr></thead><tbody id='rows'></tbody></table></div>
<div class='toast' style='margin-top:12px'>Mobil panel: http://__IP__:5000 · Yazılımsal acil durdurma, sertifikalı fiziksel emniyet rölesinin yerine geçmez.</div></div>
<script>
(function(){
let token='';let user='';
try{token=sessionStorage.getItem('process_token')||'';user=sessionStorage.getItem('process_user')||'';}catch(e){}
const esc=v=>String(v??'').replace(/[&<>\x22']/g,c=>c==='&'?'&amp;':c==='<'?'&lt;':c==='>'?'&gt;':c==='\x22'?'&quot;':'&#39;');
function setToast(type,msg){const el=document.getElementById('authMsg');if(el){el.className='toast '+(type||'');el.textContent=msg;}}
function showAuth(u){document.getElementById('loginBox').style.display='none';document.getElementById('userBar').classList.add('show');document.getElementById('loggedInUser').textContent=u;document.getElementById('commands').classList.add('show');setToast('success','✅ Yetkili yönetici oturumu aktif. Komut gönderebilirsiniz.');}
function hideAuth(){document.getElementById('loginBox').style.display='grid';document.getElementById('userBar').classList.remove('show');document.getElementById('commands').classList.remove('show');token='';user='';try{sessionStorage.removeItem('process_token');sessionStorage.removeItem('process_user');}catch(e){}setToast('','İzleme herkese açıktır; fiziksel komutlar yalnız Administrator oturumu ile çalışır.');}
if(token&&user){showAuth(user);}

async function login(){const u=document.getElementById('username').value.trim(),p=document.getElementById('password').value.trim();if(!u||!p){setToast('error','⚠️ Lütfen kullanıcı adı ve parolanızı girin.');return;}setToast('warn','⏳ Giriş yapılıyor...');try{const r=await fetch('/api/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:u,password:p})});const d=await r.json();if(!r.ok)throw new Error(d.message||'Giriş başarısız');token=d.token;user=d.username;try{sessionStorage.setItem('process_token',token);sessionStorage.setItem('process_user',user);}catch(e){}showAuth(user);}catch(e){setToast('error','❌ '+e.message);}}
async function logout(){try{await fetch('/api/logout?token='+encodeURIComponent(token),{method:'POST',headers:{Authorization:'Bearer '+token}});}catch(e){}hideAuth();}

async function sendCommand(url,label,btnEl){
if(!token){setToast('error','⚠️ Önce yönetici girişi yapmalısınız.');hideAuth();return;}
const originalText=btnEl?btnEl.textContent:label;
const btns=document.querySelectorAll('#commands button');
btns.forEach(b=>b.disabled=true);
if(btnEl)btnEl.textContent='⏳ Gönderiliyor...';
setToast('warn','⏳ '+label+' komutu gönderiliyor...');
try{
const fullUrl=url+'?token='+encodeURIComponent(token);
const r=await fetch(fullUrl,{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+token},body:JSON.stringify({token:token})});
const d=await r.json();
if(!r.ok){
if(r.status===401||r.status===403){hideAuth();throw new Error('🔒 Oturum süresi doldu. Lütfen tekrar giriş yapın.');}
if(r.status===409){throw new Error('⚠️ Cihaz Bağlı Değil! C# Masaüstü Uygulamasında COM Portuna \'Bağlan\' butonuna basın.');}
throw new Error(d.message||'Komut reddedildi');
}
setToast('success','✅ '+label.toUpperCase()+' komutu başarıyla Arduino/ESP32 cihazına iletildi.');
refresh();
}catch(e){setToast('error',e.message);}finally{if(btnEl)btnEl.textContent=originalText;btns.forEach(b=>b.disabled=false);}
}

document.getElementById('btnLogin').addEventListener('click',login);
document.getElementById('btnLogout').addEventListener('click',logout);

['btnStart','btnEstop','btnReset'].forEach(id=>{
const btn=document.getElementById(id);
if(!btn)return;
const url=id==='btnStart'?'/api/start':id==='btnEstop'?'/api/estop':'/api/reset';
const label=id==='btnStart'?'Sistemi Etkinleştir':id==='btnEstop'?'Acil Durdur':'Resetle';
const handler=function(e){e.preventDefault();sendCommand(url,label,btn);};
btn.addEventListener('click',handler);
});

async function refresh(){try{const r=await fetch('/api/stats',{cache:'no-store'});const d=await r.json();document.getElementById('total').textContent=d.total||0;document.getElementById('pass').textContent=d.pass||0;document.getElementById('fail').textContent=d.fail||0;document.getElementById('yield').textContent='%'+Number(d.yield||0).toFixed(1);document.getElementById('device').textContent=d.deviceConnected?(d.deviceName||'Bağlı'):'Cihaz bağlı değil';document.getElementById('dot').className='dot '+(d.deviceConnected?'on':'');
if(d.lastTest){const x=d.lastTest;document.getElementById('pLabel').textContent=x.primaryLabel;document.getElementById('sLabel').textContent=x.secondaryLabel;document.getElementById('pValue').textContent=x.primaryDisplay;document.getElementById('sValue').textContent=x.secondaryDisplay;const res=document.getElementById('result');res.textContent=x.result+' · '+x.serial;res.className='result '+(x.result==='PASS'?'pass':'fail');document.getElementById('detail').textContent=x.product+' · '+x.errorCode+' · '+x.errorDescription+' · '+x.time;}
const errors=document.getElementById('errors');errors.innerHTML=(d.errorSummary||[]).length?(d.errorSummary||[]).map(e=>`<div class='err'><span>${esc(e.code)} · ${esc(e.description)}</span><b>${e.count}</b></div>`).join(''):`<div class='err'><span>Aktif hata kaydı yok</span><b>0</b></div>`;
document.getElementById('rows').innerHTML=(d.recentTests||[]).map(x=>`<tr><td>${esc(x.time)}</td><td>${esc(x.serial)}</td><td>${esc(x.product)}</td><td>${esc(x.primaryDisplay)}</td><td>${esc(x.secondaryDisplay)}</td><td><span class='tag ${x.result==='PASS'?'pass':'fail'}'>${esc(x.result)}</span></td><td>${esc(x.errorCode)}</td></tr>`).join('');}catch(e){document.getElementById('device').textContent='Sunucu bağlantısı kesildi';document.getElementById('dot').className='dot';}}

refresh();setInterval(refresh,1500);
})();
</script></body></html>".Replace("__IP__", WebUtility.HtmlEncode(localIp ?? "127.0.0.1"));
        }
    }
}
