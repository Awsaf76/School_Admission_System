import { useEffect, useMemo, useState } from 'react'
import { api, Hold, SchoolClass } from './api'

function App() {
  const [tab,setTab] = useState<'parent'|'admin'>('parent')
  const [studentId,setStudentId] = useState('')
  const [classes,setClasses] = useState<SchoolClass[]>([])
  const [holds,setHolds] = useState<Hold[]>([])
  const [className,setClassName] = useState('')
  const [totalSeats,setTotalSeats] = useState('30')
  const [message,setMessage] = useState('')
  const [busy,setBusy] = useState(false)

  async function loadClasses() {
    try { const r = await api.get<SchoolClass[]>('/classes'); setClasses(r.data) }
    catch { setMessage('Cannot reach API. Check that Docker services are running.') }
  }
  async function loadHolds() {
    if (!studentId.trim()) { setHolds([]); return }
    try { const r = await api.get<Hold[]>('/me/holds',{headers:{'X-Student-Id':studentId.trim()}}); setHolds(r.data) }
    catch { setHolds([]) }
  }
  useEffect(() => {
    loadClasses()
    const timer = window.setInterval(() => { loadClasses(); if(studentId.trim()) loadHolds() },3000)
    return () => window.clearInterval(timer)
  },[studentId])

  async function createClass(e:React.FormEvent) {
    e.preventDefault(); setBusy(true); setMessage('')
    try { await api.post('/classes',{name:className,totalSeats:Number(totalSeats)}); setClassName(''); setMessage('Class created.'); await loadClasses() }
    catch(err:any) { setMessage(err.response?.data?.message || 'Could not create class.') }
    finally { setBusy(false) }
  }
  async function apply(classId:string) {
    if(!studentId.trim()) { setMessage('Enter the student ID first.'); return }
    setBusy(true); setMessage('')
    try { await api.post(`/classes/${classId}/holds`,{},{headers:{'X-Student-Id':studentId.trim()}}); setMessage('Seat held. Complete the simulated payment before the timer expires.'); await loadHolds(); await loadClasses() }
    catch(err:any) { setMessage(err.response?.data?.message || 'Application failed.') }
    finally { setBusy(false) }
  }
  async function confirm(holdId:string) {
    setBusy(true); setMessage('')
    try { await api.post(`/holds/${holdId}/confirm`,{},{headers:{'X-Student-Id':studentId.trim()}}); setMessage('Admission confirmed!'); await loadHolds(); await loadClasses() }
    catch(err:any) { setMessage(err.response?.data?.message || 'Confirmation failed.'); await loadHolds() }
    finally { setBusy(false) }
  }
  async function cancel(holdId:string) {
    setBusy(true); setMessage('')
    try { await api.delete(`/holds/${holdId}`,{headers:{'X-Student-Id':studentId.trim()}}); setMessage('Reservation cancelled and seat released.'); await loadHolds(); await loadClasses() }
    catch(err:any) { setMessage(err.response?.data?.message || 'Cancellation failed.') }
    finally { setBusy(false) }
  }

  return <div className="shell">
    <header><div className="brand"><b>SA</b><div><strong>School Admission</strong><small>Admission management portal</small></div></div><span className="online">● System online</span></header>
    <main>
      <section className="hero"><div><label>ADMISSIONS · 2026–2027</label><h1>Welcome to the admission portal</h1><p>Apply for a seat, track your reservation and complete admission.</p></div><span>🎓</span></section>
      <nav><button className={tab==='parent'?'selected':''} onClick={()=>setTab('parent')}>Parent / Student</button><button className={tab==='admin'?'selected':''} onClick={()=>setTab('admin')}>Admin dashboard</button></nav>
      {message && <div className="notice">{message}<button onClick={()=>setMessage('')}>×</button></div>}
      {tab==='parent' ? <>
        <section className="panel id-panel"><div><h2>Student identification</h2><p>Enter a student ID to apply and view application history.</p></div><label>Student ID<input value={studentId} onChange={e=>setStudentId(e.target.value)} placeholder="Enter student ID" maxLength={40}/></label></section>
        <div className="heading"><div><small>AVAILABLE OPTIONS</small><h2>Classes and shifts</h2></div><button className="secondary" onClick={loadClasses}>↻ Refresh</button></div>
        <div className="cards">{classes.map(c=><article className="class-card" key={c.id}><div className="card-top"><span className="tag">ADMISSION OPEN</span><span>{c.availableSeats} seats left</span></div><h3>{c.name}</h3><div className="bar"><i style={{width:`${c.totalSeats?((c.heldSeats+c.confirmedSeats)/c.totalSeats)*100:0}%`}}/></div><div className="numbers"><span><b>{c.totalSeats}</b>Total</span><span><b>{c.heldSeats}</b>Held</span><span><b>{c.confirmedSeats}</b>Confirmed</span></div><button className="primary wide" disabled={busy||c.availableSeats===0} onClick={()=>apply(c.id)}>{c.availableSeats===0?'No seats available':'Apply & hold a seat'}</button></article>)}{classes.length===0&&<p className="empty">No classes created yet. Ask the admin to add a class.</p>}</div>
        <div className="heading"><div><small>APPLICATION TRACKER</small><h2>My applications</h2></div><button className="secondary" onClick={loadHolds}>↻ Refresh</button></div>
        <section className="panel">{holds.length===0?<p className="muted">Enter your student ID to view your applications.</p>:holds.map(h=><HoldRow key={h.holdId} hold={h} busy={busy} confirm={confirm} cancel={cancel}/>)}</section>
      </> : <>
        <div className="heading"><div><small>SCHOOL MANAGEMENT</small><h2>Admin dashboard</h2><p className="muted">Create class shifts and monitor seat allocation.</p></div></div>
        <div className="admin-grid"><form className="panel form" onSubmit={createClass}><h3>Create a class / shift</h3><label>Class / shift name<input required maxLength={120} value={className} onChange={e=>setClassName(e.target.value)} placeholder="Class 6 - Morning Shift"/></label><label>Total seats<input required type="number" min="1" max="100000" value={totalSeats} onChange={e=>setTotalSeats(e.target.value)}/></label><button className="primary" disabled={busy}>Create class</button></form>
        <section className="panel"><h3>Seat overview</h3><div className="big-number">{classes.length}</div><p className="muted">Classes / shifts configured</p><div className="mini"><span>Available <b>{classes.reduce((n,c)=>n+c.availableSeats,0)}</b></span><span>Held <b>{classes.reduce((n,c)=>n+c.heldSeats,0)}</b></span><span>Confirmed <b>{classes.reduce((n,c)=>n+c.confirmedSeats,0)}</b></span></div></section></div>
        <section className="panel table-panel"><h3>Class capacity</h3><div className="table-scroll"><table><thead><tr><th>Class / shift</th><th>Total</th><th>Held</th><th>Confirmed</th><th>Available</th></tr></thead><tbody>{classes.map(c=><tr key={c.id}><td>{c.name}</td><td>{c.totalSeats}</td><td>{c.heldSeats}</td><td>{c.confirmedSeats}</td><td><b>{c.availableSeats}</b></td></tr>)}{classes.length===0&&<tr><td colSpan={5}>No classes created yet.</td></tr>}</tbody></table></div></section>
      </>}
      <footer>School Admission System · Demonstration project · Payment is simulated</footer>
    </main>
  </div>
}

function HoldRow({hold,busy,confirm,cancel}:{hold:Hold;busy:boolean;confirm:(id:string)=>void;cancel:(id:string)=>void}) {
  const [seconds,setSeconds]=useState(Math.max(0,Math.ceil((new Date(hold.expiresAt).getTime()-Date.now())/1000)))
  useEffect(()=>{const t=window.setInterval(()=>setSeconds(Math.max(0,Math.ceil((new Date(hold.expiresAt).getTime()-Date.now())/1000))),1000);return()=>clearInterval(t)},[hold.expiresAt])
  return <div className="hold-row"><div><strong>{hold.className||hold.classId}</strong><small>Reference: {hold.holdId}</small>{hold.status==='HELD'&&<small className="timer">{seconds?`Time remaining: ${Math.floor(seconds/60)}:${String(seconds%60).padStart(2,'0')}`:'Checking expiry status…'}</small>}</div><div className="hold-actions"><span className={`status ${hold.status.toLowerCase()}`}>{hold.status}</span>{hold.status==='HELD'&&<><button className="primary small" disabled={busy||seconds===0} onClick={()=>confirm(hold.holdId)}>Pay admission fee</button><button className="text-button" disabled={busy} onClick={()=>cancel(hold.holdId)}>Cancel</button></>}</div></div>
}
export default App
