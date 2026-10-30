async function request(url, options) {
  const response = await fetch(url, options);
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    const fields = body?.errors ? Object.values(body.errors).flat().join(' ') : '';
    throw new Error(fields || body?.detail || body?.title || `Помилка сервера (${response.status})`);
  }
  return body;
}

export function listPoints({ query, status, skip, limit }) {
  const params = new URLSearchParams({ skip, limit });
  if (status) params.set('status', status);
  if (query) {
    params.set('q', query);
    return request(`/api/search?${params}`);
  }
  return request(`/api/points?${params}`);
}

export const getPoint = (id) => request(`/api/points/${id}`);

export const getReports = (id) => request(`/api/points/${id}/reports?limit=10`);

export const addReport = (token, report) =>
  request('/api/reports', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(report)
  });
