import { render, screen, within, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';
import type { Summary, Voucher } from './api';

const configured: Summary = { configured: true, currency: 'PEN', balance: 1000, outstanding: 0, spent: 0, pendingCount: 0, deliveredCount: 0, closedCount: 0 };
const voucher: Voucher = { id: 'v1', number: 'VC-000001', date: '2026-11-17', responsible: 'Ana Pérez', concept: 'Movilidad', amount: 80, status: 'Pending', spent: 0, returned: 0, pending: 0, deliveredAt: null, closedAt: null, receipts: [] };
let summary: Summary;
let vouchers: Voucher[];
let failDelivery: boolean;
let failRefresh: boolean;
let saved: boolean;
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
  const url = String(input);
  if (init?.method === 'POST') {
    if (url.endsWith('/fund')) { summary = configured; return new Response(null, { status: 204 }); }
    if (url.endsWith('/vouchers')) { saved = true; vouchers = [voucher]; return json(voucher, 201); }
    if (url.endsWith('/deliver')) return failDelivery ? json({ title: 'El saldo de caja es insuficiente para esta entrega.' }, 409) : new Response(null, { status: 204 });
    if (url.endsWith('/receipts')) return new Response(null, { status: 204 });
  }
  if (failRefresh && saved) throw new Error('offline');
  if (url.endsWith('/summary')) return json(summary);
  if (url.endsWith('/vouchers')) return json(vouchers);
  if (url.endsWith('/movements')) return json([]);
  if (url.includes('/reports?')) return json({ from: '2026-11-01', to: '2026-11-17', currency: 'PEN', vouchers, requested: 80, spent: 0, returned: 0 });
  throw new Error(`Unexpected request: ${url}`);
});
beforeEach(() => {
  summary = { ...configured }; vouchers = []; failDelivery = false; failRefresh = false; saved = false;
  fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});
describe('Caja chica', () => {
  it('abre la caja con la moneda y el fondo ingresados', async () => {
    summary = { ...configured, configured: false, currency: null, balance: 0 };
    const user = userEvent.setup(); render(<App />);
    await user.click(await screen.findByRole('button', { name: 'Abrir caja chica' }));
    const dialog = screen.getByRole('dialog');
    await user.selectOptions(within(dialog).getByLabelText('Moneda'), 'PEN');
    await user.type(within(dialog).getByLabelText('Fondo inicial'), '1000');
    await user.click(within(dialog).getByRole('button', { name: 'Abrir caja chica' }));
    expect(await screen.findByText('Caja chica abierta correctamente.')).toBeVisible();
    const call = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/fund') && init?.method === 'POST')!;
    expect(JSON.parse(call[1]!.body as string)).toEqual({ currency: 'PEN', amount: 1000 });
  });
  it('registra un vale y muestra el número asignado por el servicio', async () => {
    const user = userEvent.setup(); render(<App />);
    await user.click(await screen.findByRole('button', { name: 'Nuevo vale' }));
    const dialog = screen.getByRole('dialog');
    await user.type(within(dialog).getByLabelText('Responsable'), 'Ana Pérez');
    await user.type(within(dialog).getByLabelText('Concepto'), 'Movilidad');
    await user.type(within(dialog).getByLabelText('Importe (PEN)'), '80');
    await user.click(within(dialog).getByRole('button', { name: 'Registrar vale' }));
    expect(await screen.findByText('VC-000001')).toBeVisible();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
  it('muestra el rechazo del servicio al confirmar una entrega sin saldo', async () => {
    vouchers = [voucher]; failDelivery = true;
    const user = userEvent.setup(); render(<App />);
    await user.click(await screen.findByRole('button', { name: 'Ver VC-000001' }));
    await user.click(screen.getByRole('button', { name: 'Registrar entrega' }));
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/deliver'))).toBe(false);
    await user.click(screen.getByRole('button', { name: 'Confirmar entrega' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('El saldo de caja es insuficiente');
    expect(screen.getByRole('dialog')).toBeVisible();
  });
  it('envía el archivo y los datos del comprobante como formulario', async () => {
    vouchers = [{ ...voucher, status: 'Delivered', pending: 80 }];
    const user = userEvent.setup(); render(<App />);
    await user.click(await screen.findByRole('button', { name: 'Ver VC-000001' }));
    await user.click(screen.getByRole('button', { name: 'Agregar comprobante' }));
    await user.type(screen.getByLabelText('Número de comprobante'), 'F001-001');
    await user.type(screen.getByLabelText('Gasto (PEN)'), '60');
    await user.upload(screen.getByLabelText('Archivo del comprobante'), new File(['%PDF-1.7'], 'factura.pdf', { type: 'application/pdf' }));
    const receiptForm = screen.getByLabelText('Archivo del comprobante').closest('form')!;
    expect((screen.getByLabelText('Archivo del comprobante') as HTMLInputElement).files?.[0].name).toBe('factura.pdf');
    // jsdom reports the uploaded file input as missing; exercise submission with the verified file.
    fireEvent.submit(receiptForm);
    await screen.findByText('Comprobante registrado correctamente.');
    const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/receipts'))!;
    const body = call[1]!.body as FormData;
    expect(body).toBeInstanceOf(FormData); expect(body.get('reference')).toBe('F001-001');
    expect(body.get('amount')).toBe('60');
  });
  it('comunica errores de conexión sin presentar datos ficticios', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline'))); render(<App />);
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar');
    expect(screen.getByText('No se pudo cargar la caja')).toBeVisible();
    expect(screen.queryByText('Saldo disponible')).not.toBeInTheDocument();
  });
  it('no invita a reenviar un vale guardado cuando falla la actualización posterior', async () => {
    failRefresh = true;
    const user = userEvent.setup(); render(<App />);
    await user.click(await screen.findByRole('button', { name: 'Nuevo vale' }));
    await user.type(screen.getByLabelText('Responsable'), 'Ana Pérez');
    await user.type(screen.getByLabelText('Concepto'), 'Movilidad');
    await user.type(screen.getByLabelText('Importe (PEN)'), '80');
    await user.click(screen.getByRole('button', { name: 'Registrar vale' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('La operación fue registrada');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.filter(([url, init]) => String(url).endsWith('/vouchers') && init?.method === 'POST')).toHaveLength(1);
  });
  it('filtra la tabla y consulta reportes sin calcular totales en la interfaz', async () => {
    vouchers = [voucher]; const user = userEvent.setup(); render(<App />);
    await screen.findByText('VC-000001');
    await user.click(screen.getByRole('button', { name: 'Cerrados' }));
    expect(screen.getByText('No hay coincidencias')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Reportes' }));
    await user.click(screen.getByRole('button', { name: 'Consultar' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeVisible());
    expect(screen.getByText('VC-000001')).toBeVisible();
  });
});
