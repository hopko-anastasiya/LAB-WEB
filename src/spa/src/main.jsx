import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { HashRouter, Link, Route, Routes } from 'react-router-dom';
import Catalog from './Catalog.jsx';
import PointPage from './PointPage.jsx';
import './styles.css';

function App() {
  return (
    <HashRouter>
      <header className="top">
        <Link to="/" className="brand"><span className="brand-lamp" aria-hidden="true" />Незламні</Link>
        <a href="/">Повний сайт</a>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Catalog />} />
          <Route path="/points/:id" element={<PointPage />} />
          <Route path="*" element={<p>Такої сторінки немає. <Link to="/">До каталогу</Link></p>} />
        </Routes>
      </main>
    </HashRouter>
  );
}

createRoot(document.getElementById('root')).render(<StrictMode><App /></StrictMode>);
