export interface Receipt { id: string; date: string; reference: string; amount: number; fileName: string }
export interface Voucher { id: string; number: string; date: string; responsible: string; concept: string; amount: number; status: 'Pending' | 'Delivered' | 'Closed'; spent: number; returned: number; pending: number; deliveredAt: string | null; closedAt: string | null; receipts: Receipt[] }
export interface Summary { configured: boolean; currency: string | null; balance: number; outstanding: number; spent: number; pendingCount: number; deliveredCount: number; closedCount: number }
export interface Movement { id: string; at: string; type: string; detail: string; voucherId: string | null; amount: number; balance: number }
export interface Report { from: string; to: string; currency: string; vouchers: Voucher[]; requested: number; spent: number; returned: number }
export async function request(path: string, init?: RequestInit): Promise<Response> {
  const base = import.meta.env.VITE_API_URL;
  if (!base) throw new Error('Falta configurar la dirección del servicio de caja chica.');
  let response: Response;
  try { response = await fetch(`${base.replace(/\/$/, '')}/api${path}`, init); }
  catch { throw new Error('No se pudo conectar con caja chica. Revise la conexión y actualice la vista antes de repetir una operación.'); }
  if (!response.ok) {
    const text = await response.text();
    let message = `El servicio respondió con un error (${response.status}).`;
    if (text) {
      try { const problem = JSON.parse(text); message = problem.detail || problem.title || message; }
      catch { message = `El servicio no pudo procesar la operación (${response.status}).`; }
    }
    throw new Error(message);
  }
  return response;
}
export async function read<T>(path: string): Promise<T> { return (await request(path)).json(); }
export function post(path: string, body: object) { return request(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); }
export async function download(path: string, filename: string) {
  const response = await request(path); const blob = await response.blob();
  const url = URL.createObjectURL(blob); const link = document.createElement('a');
  link.href = url; link.download = filename; document.body.appendChild(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
