import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';
afterEach(cleanup);
HTMLDialogElement.prototype.showModal = function() { this.open = true; };
HTMLDialogElement.prototype.close = function() { this.open = false; };
vi.stubEnv('VITE_API_URL', 'http://localhost:5100');
