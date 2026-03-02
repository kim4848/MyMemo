import 'fake-indexeddb/auto';
import { describe, test, expect, beforeEach } from 'vitest';
import { ChunkCache } from './chunk-cache';

let cache: ChunkCache;

beforeEach(async () => {
  // Use a unique DB name per test to avoid state leaking
  cache = new ChunkCache(`test-db-${Math.random()}`);
});

describe('ChunkCache', () => {
  test('stores and retrieves a pending chunk', async () => {
    const blob = new Blob(['audio-data'], { type: 'audio/webm' });
    await cache.store('sess1', 0, blob);

    const pending = await cache.getPending('sess1');
    expect(pending).toHaveLength(1);
    expect(pending[0].sessionId).toBe('sess1');
    expect(pending[0].chunkIndex).toBe(0);
    expect(pending[0].blob).toBeDefined();
  });

  test('marks chunk as uploaded and removes from pending', async () => {
    const blob = new Blob(['audio-data']);
    await cache.store('sess1', 0, blob);
    await cache.markUploaded('sess1', 0);

    const pending = await cache.getPending('sess1');
    expect(pending).toHaveLength(0);
  });

  test('stores multiple chunks in order', async () => {
    await cache.store('sess1', 0, new Blob(['a']));
    await cache.store('sess1', 1, new Blob(['b']));
    await cache.store('sess1', 2, new Blob(['c']));

    const pending = await cache.getPending('sess1');
    expect(pending).toHaveLength(3);
    expect(pending.map((p) => p.chunkIndex)).toEqual([0, 1, 2]);
  });

  test('clearSession removes all chunks for a session', async () => {
    await cache.store('sess1', 0, new Blob(['a']));
    await cache.store('sess1', 1, new Blob(['b']));
    await cache.store('sess2', 0, new Blob(['c']));

    await cache.clearSession('sess1');

    const s1 = await cache.getPending('sess1');
    const s2 = await cache.getPending('sess2');
    expect(s1).toHaveLength(0);
    expect(s2).toHaveLength(1);
  });
});
