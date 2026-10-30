import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { addReport, getPoint, getReports } from './api.js';
import Status, { STATUSES } from './Status.jsx';

const dateFormat = new Intl.DateTimeFormat('uk-UA', { dateStyle: 'short', timeStyle: 'short' });

function ReportForm({ pointId, currentStatus, onAdded }) {
  const [token, setToken] = useState('');
  const [form, setForm] = useState({ status: currentStatus, hasElectricity: true, hasInternet: true, hasHeating: true, occupancy: 0, comment: '' });
  const [message, setMessage] = useState(null);
  const [busy, setBusy] = useState(false);

  const set = (name, value) => setForm((f) => ({ ...f, [name]: value }));

  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setMessage(null);
    try {
      await addReport(token.trim(), { ...form, pointId, occupancy: Number(form.occupancy) });
      setMessage({ ok: true, text: 'Звіт додано.' });
      set('comment', '');
      onAdded();
    } catch (err) {
      setMessage({ ok: false, text: err.message });
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="report-form" onSubmit={submit}>
      <h2>Додати звіт</h2>
      <p className="muted">Потрібен API-токен із профілю на сайті. Токен не зберігається.</p>
      <label>API-токен
        <input type="password" value={token} onChange={(e) => setToken(e.target.value)} required autoComplete="off" />
      </label>
      <label>Статус
        <select value={form.status} onChange={(e) => set('status', e.target.value)}>
          {STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
      </label>
      <div className="checks">
        {[['hasElectricity', 'Світло'], ['hasInternet', 'Зв\'язок'], ['hasHeating', 'Тепло']].map(([name, label]) => (
          <label key={name}>
            <input type="checkbox" checked={form[name]} onChange={(e) => set(name, e.target.checked)} /> {label}
          </label>
        ))}
      </div>
      <label>Людей у пункті
        <input type="number" min="0" max="10000" value={form.occupancy} onChange={(e) => set('occupancy', e.target.value)} />
      </label>
      <label>Коментар
        <textarea rows="2" maxLength="1000" value={form.comment} onChange={(e) => set('comment', e.target.value)} />
      </label>
      <button type="submit" disabled={busy}>{busy ? 'Надсилаємо…' : 'Додати звіт'}</button>
      {message && <p className={message.ok ? 'success' : 'error'} role="status">{message.text}</p>}
    </form>
  );
}

export default function PointPage() {
  const { id } = useParams();
  const [point, setPoint] = useState(null);
  const [reports, setReports] = useState([]);
  const [error, setError] = useState(null);

  const load = useCallback(() => {
    setError(null);
    return Promise.all([getPoint(id), getReports(id)])
      .then(([p, r]) => { setPoint(p); setReports(r.items); })
      .catch((err) => setError(err.message));
  }, [id]);

  useEffect(() => { load(); }, [load]);

  if (error) return <><p className="error" role="alert">{error}</p><Link to="/">До каталогу</Link></>;
  if (!point) return <p className="muted">Завантажуємо пункт…</p>;

  return (
    <>
      <Link to="/">← Каталог</Link>
      <h1>{point.name}</h1>
      <Status value={point.status} />
      <p>{point.settlement}, {point.address}</p>
      {point.description && <p className="muted">{point.description}</p>}
      {point.services.length > 0 && (
        <ul className="chips">{point.services.map((s) => <li key={s.id}>{s.name}</li>)}</ul>
      )}
      <p><a href={`/punkt/${point.id}`}>Відкрити сторінку пункту на сайті</a></p>

      <div className="columns">
        <section>
          <h2>Останні звіти</h2>
          {reports.length === 0 && <p className="muted">Звітів ще немає.</p>}
          <ul className="rows">
            {reports.map((r) => (
              <li key={r.id} className="report">
                <span className="row-main">
                  <b>{dateFormat.format(new Date(r.createdAt))}</b>
                  <small>Людей: {r.occupancy}. {r.hasElectricity ? 'Є світло' : 'Без світла'}.</small>
                  {r.comment && <small>{r.comment}</small>}
                </span>
                <Status value={r.status} />
              </li>
            ))}
          </ul>
        </section>
        <ReportForm pointId={point.id} currentStatus={point.status} onAdded={load} />
      </div>
    </>
  );
}
