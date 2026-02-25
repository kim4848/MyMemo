import { openDB, type IDBPDatabase } from 'idb';

interface CachedChunk {
  sessionId: string;
  chunkIndex: number;
  blob: Blob;
  createdAt: number;
}

const STORE_NAME = 'chunks';

export class ChunkCache {
  private dbPromise: Promise<IDBPDatabase>;

  constructor(dbName = 'mymemo-chunks') {
    this.dbPromise = openDB(dbName, 1, {
      upgrade(db) {
        const store = db.createObjectStore(STORE_NAME, {
          keyPath: ['sessionId', 'chunkIndex'],
        });
        store.createIndex('bySession', 'sessionId');
      },
    });
  }

  async store(sessionId: string, chunkIndex: number, blob: Blob) {
    const db = await this.dbPromise;
    await db.put(STORE_NAME, {
      sessionId,
      chunkIndex,
      blob,
      createdAt: Date.now(),
    });
  }

  async getPending(sessionId: string): Promise<CachedChunk[]> {
    const db = await this.dbPromise;
    const all = await db.getAllFromIndex(STORE_NAME, 'bySession', sessionId);
    return all.sort((a, b) => a.chunkIndex - b.chunkIndex);
  }

  async markUploaded(sessionId: string, chunkIndex: number) {
    const db = await this.dbPromise;
    await db.delete(STORE_NAME, [sessionId, chunkIndex]);
  }

  async clearSession(sessionId: string) {
    const db = await this.dbPromise;
    const tx = db.transaction(STORE_NAME, 'readwrite');
    const index = tx.store.index('bySession');
    let cursor = await index.openCursor(sessionId);
    while (cursor) {
      await cursor.delete();
      cursor = await cursor.continue();
    }
    await tx.done;
  }
}
