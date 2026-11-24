import path from 'node:path';
import { DhakaAuthoritativeServer } from './authoritativeServer';
import { AccountStore } from './accountStore';

const PORT = parseInt(process.env.PORT || '8080', 10);
const ACCOUNTS_FILE = process.env.ACCOUNTS_FILE || path.resolve(process.cwd(), 'data/dhaka_accounts.json');

const accountStore = new AccountStore(ACCOUNTS_FILE);
const server = new DhakaAuthoritativeServer(accountStore);

server.listen(PORT).then(() => {
  console.log(`[DhakaServer] Server ready for 2-16 simultaneous players.`);
  console.log(`[DhakaServer] WebSocket endpoint: ws://0.0.0.0:${PORT}`);
  console.log(`[DhakaServer] Health check: http://0.0.0.0:${PORT}/health`);
});

const onShutdown = async (signal: string) => {
  console.log(`[DhakaServer] Received ${signal}. Saving state and stopping server...`);
  await server.stop();
  process.exit(0);
};

process.on('SIGINT', () => onShutdown('SIGINT'));
process.on('SIGTERM', () => onShutdown('SIGTERM'));
