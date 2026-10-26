import QRCode from 'qrcode';
import { setTimeout as pause } from 'node:timers/promises';
import { dataDirectory, gatewayToken } from './storage.mjs';

const action = process.argv[2] ?? 'status';
const port = Number(process.env.LUMAR_LOCAL_GATEWAY_PORT ?? 5094);
const endpoint = `http://127.0.0.1:${port}`;
const token = gatewayToken(dataDirectory());
async function call(path, body) {
  const response = await fetch(endpoint + path, { method: body === undefined ? 'GET' : 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(35000) });
  if (!response.ok) throw new Error('LOCAL_GATEWAY_REQUEST_FAILED');
  return response.json();
}

try {
  if (['link', 'replace'].includes(action)) {
    if (!process.stdout.isTTY) throw new Error('INTERACTIVE_TERMINAL_REQUIRED_FOR_QR');
    await call(`/session/${action}`, {});
    console.log('اربط حساب الاختبار المصرح به فقط من واتساب > الأجهزة المرتبطة. لا تحفظ رمز الربط أو تشاركه.');
    let previous = null;
    const deadline = Date.now() + 180000;
    while (Date.now() < deadline) {
      const status = await call('/session/status');
      if (status.state === 'Connected') {
        console.clear();
        console.log('تم الاتصال بحساب الاختبار. لم يتم تفعيل الإرسال التشغيلي.');
        process.exit(0);
      }
      if (['Error', 'LoggedOut', 'Disconnected'].includes(status.state)) throw new Error('SESSION_NOT_READY');
      const material = await call('/session/link-material');
      if (material.kind === 'qr' && material.value !== previous && Date.parse(material.expiresAt) > Date.now()) {
        console.clear();
        console.log(await QRCode.toString(material.value, { type: 'terminal', small: true }));
        console.log('امسح الرمز بحساب الاختبار المصرح به فقط.');
        previous = material.value;
      }
      await pause(1500);
    }
    console.clear();
    throw new Error('LINK_WINDOW_EXPIRED');
  } else if (['disconnect', 'reconnect', 'logout'].includes(action)) {
    const status = await call(`/session/${action}`, {});
    console.log(JSON.stringify({ state: status.state, operationalActivation: false }));
  } else if (action === 'status') {
    const health = await call('/health');
    const status = await call('/session/status');
    console.log(JSON.stringify({ health, ...status, currentNumber: status.currentNumber ? `***${status.currentNumber.slice(-4)}` : null }, null, 2));
  } else {
    throw new Error('UNSUPPORTED_OWNER_ACTION');
  }
} catch {
  console.error('تعذر إكمال الطلب المحلي. افحص تشغيل البوابة وحالة الجلسة؛ لا ترسل الأسرار أو QR إلى المحادثة.');
  process.exitCode = 1;
}