import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { listPoints } from './api.js';
import Status, { STATUSES } from './Status.jsx';

const PAGE = 10;

export default function Catalog() {
  const [params, setParams] = useSearchParams();
  const query = params.get('q') ?? '';
  const status = params.get('status') ?? '';
  const skip = Number(params.get('skip')) || 0;

  const [text, setText] = useState(query);
  const [page, setPage] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    let active = true;
    setError(null);
    listPoints({ query, status, skip, limit: PAGE })
      .then((result) => { if (active) setPage(result); })
      .catch((err) => { if (active) setError(err.message); });
    return () => { active = false; };
  }, [query, status, skip]);

  function update(next) {
    const merged = { q: query, status, ...next };
    setParams(Object.fromEntries(Object.entries(merged).filter(([, v]) => v)));
  }

  function submit(event) {
    event.preventDefault();
    update({ q: text.trim(), skip: '' });
  }

  return (
    <>
      <h1>Каталог пунктів</h1>
      <form className="filters" onSubmit={submit}>
        <input
          type="search"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Назва, місто, послуга"
          aria-label="Пошук пунктів"
        />
        <select value={status} onChange={(e) => update({ status: e.target.value, skip: '' })} aria-label="Статус">
          <option value="">Будь-який статус</option>
          {STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
        <button type="submit">Знайти</button>
      </form>

      {error && <p className="error" role="alert">{error}</p>}
      {!error && !page && <p className="muted">Завантажуємо пункти…</p>}
      {page && page.items.length === 0 && <p className="muted">Таких пунктів немає. Спробуйте інше слово або приберіть фільтр.</p>}

      {page && page.items.length > 0 && (
        <>
          <p className="muted">Знайдено: {page.total}</p>
          <ul className="rows">
            {page.items.map((p) => (
              <li key={p.id}>
                <Link to={`/points/${p.id}`}>
                  <span className="row-main">
                    <b>{p.name}</b>
                    <small>{p.settlement}, {p.address}</small>
                  </span>
                  <Status value={p.status} />
                </Link>
              </li>
            ))}
          </ul>
          <nav className="pager" aria-label="Сторінки">
            <button type="button" disabled={skip === 0} onClick={() => update({ skip: String(Math.max(0, skip - PAGE)) })}>Попередні</button>
            <span className="muted">{skip + 1}–{skip + page.items.length} з {page.total}</span>
            <button type="button" disabled={!page.nextLink} onClick={() => update({ skip: String(skip + PAGE) })}>Наступні</button>
          </nav>
        </>
      )}
    </>
  );
}
