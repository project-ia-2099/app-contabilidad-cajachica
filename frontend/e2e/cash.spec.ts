import { test, expect } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import type { Summary, Voucher } from '../src/api';

test('abre, entrega, adjunta un PDF real y cierra un vale en el navegador', async ({ page }, testInfo) => {
  let summary: Summary = { configured: false, currency: null, balance: 0, outstanding: 0, spent: 0, pendingCount: 0, deliveredCount: 0, closedCount: 0 };
  let vouchers: Voucher[] = [];
  let uploaded = false;
  const errors: string[] = [];
  page.on('pageerror', e => errors.push(e.message));
  // Controlled API fixtures: this browser test never writes into the accounting database.
  await page.route('**/api/**', async route => {
    const req = route.request(); const path = new URL(req.url()).pathname;
    if (req.method() === 'POST') {
      if (path === '/api/fund') summary = { ...summary, configured: true, currency: 'PEN', balance: 1000 };
      else if (path === '/api/vouchers') {
        const body = req.postDataJSON();
        vouchers = [{ ...body, id: 'v1', number: 'VC-000001', status: 'Pending', spent: 0, returned: 0, pending: 0, receipts: [], deliveredAt: null, closedAt: null }];
        summary.pendingCount = 1;
        await route.fulfill({ status: 201, json: vouchers[0] }); return;
      } else if (path.endsWith('/deliver')) {
        vouchers[0] = { ...vouchers[0], status: 'Delivered', pending: 80, deliveredAt: '2026-11-17T12:00:00Z' };
        summary = { ...summary, balance: 920, outstanding: 80, pendingCount: 0, deliveredCount: 1 };
      } else if (path.endsWith('/receipts')) {
        const data = req.postDataBuffer()!.toString();
        expect(req.headers()['content-type']).toContain('multipart/form-data');
        expect(data).toContain('filename="factura.pdf"'); expect(data).toContain('%PDF-1.7'); expect(data).toContain('F001-001');
        uploaded = true;
        vouchers[0] = { ...vouchers[0], spent: 60, pending: 20, receipts: [{ id: 'r1', date: '2026-11-17', reference: 'F001-001', amount: 60, fileName: 'factura.pdf' }] };
        summary.spent = 60;
      } else if (path.endsWith('/close')) {
        expect(req.postDataJSON()).toEqual({ returned: 20 });
        vouchers[0] = { ...vouchers[0], status: 'Closed', returned: 20, pending: 0, closedAt: '2026-11-17T13:00:00Z' };
        summary = { ...summary, balance: 940, outstanding: 0, deliveredCount: 0, closedCount: 1 };
      } else throw new Error(`Unexpected operation ${path}`);
      await route.fulfill({ status: 204 }); return;
    }
    if (path === '/api/summary') await route.fulfill({ json: summary });
    else if (path === '/api/vouchers') await route.fulfill({ json: vouchers });
    else if (path === '/api/movements') await route.fulfill({ json: [] });
    else if (path === '/api/reports') await route.fulfill({ json: { from: '2026-11-01', to: '2026-11-17', currency: 'PEN', vouchers, requested: 80, spent: 60, returned: 20 } });
    else if (path === '/api/reports/export') await route.fulfill({ contentType: 'text/csv', body: 'Numero,Importe\r\nVC-000001,80.00\r\n' });
    else throw new Error(`Unexpected request ${path}`);
  });
  await page.goto('/');
  await page.getByRole('button', { name: 'Abrir caja chica', exact: true }).click();
  await page.getByLabel('Moneda').selectOption('PEN'); await page.getByLabel('Fondo inicial').fill('1000');
  await page.getByRole('dialog').getByRole('button', { name: 'Abrir caja chica', exact: true }).click();
  await expect(page.getByText('Caja chica abierta correctamente.')).toBeVisible();
  await page.getByRole('button', { name: 'Nuevo vale', exact: true }).click();
  await page.getByLabel('Responsable').fill('Ana Pérez'); await page.getByLabel('Concepto').fill('Movilidad para trámites');
  await page.getByLabel('Importe (PEN)').fill('80'); await page.getByRole('button', { name: 'Registrar vale', exact: true }).click();
  await page.getByRole('button', { name: 'Ver VC-000001' }).click();
  await page.getByRole('button', { name: 'Registrar entrega', exact: true }).click();
  await page.getByRole('button', { name: 'Confirmar entrega', exact: true }).click();
  await expect(page.getByText('Entrega de dinero registrada.')).toBeVisible();
  await page.getByRole('button', { name: 'Ver VC-000001' }).click();
  await page.getByRole('button', { name: 'Agregar comprobante', exact: true }).click();
  await page.getByLabel('Número de comprobante').fill('F001-001'); await page.getByLabel('Gasto (PEN)').fill('60');
  await page.getByLabel('Archivo del comprobante').setInputFiles({ name: 'factura.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7\ncomprobante de prueba\n%%EOF') });
  await page.getByRole('button', { name: 'Guardar comprobante', exact: true }).click();
  await expect(page.getByText('Comprobante registrado correctamente.')).toBeVisible(); expect(uploaded).toBe(true);
  await page.getByRole('button', { name: 'Ver VC-000001' }).click();
  await expect(page.getByText('F001-001', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Cerrar rendición', exact: true }).click();
  await page.getByLabel('Devolución recibida (PEN)').fill('20'); await page.getByRole('button', { name: 'Confirmar cierre' }).click();
  await expect(page.getByText('Rendición cerrada y devolución registrada.')).toBeVisible();
  await expect(page.getByText(/940,00.*PEN/)).toBeVisible();
  await expect(page.getByText('Cerrado', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await mkdir('work', { recursive: true }); await page.screenshot({ path: `work/${testInfo.project.name}.png`, fullPage: true });
  await page.getByRole('button', { name: 'Reportes', exact: true }).click();
  await page.getByRole('button', { name: 'Consultar', exact: true }).click();
  const downloadEvent = page.waitForEvent('download'); await page.getByRole('button', { name: 'Exportar CSV', exact: true }).click();
  expect((await downloadEvent).suggestedFilename()).toMatch(/^vales-.*\.csv$/);
  expect(errors).toEqual([]);
});
