import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinsStore } from './pinsStore';

// There is no localStorage in Node, so persist warns. We only test the logic here.
beforeEach(() => {
  vi.spyOn(console, 'warn').mockImplementation(() => undefined);
});

describe('pins store', () => {
  it('pins and unpins, keeping ONLY id and name', () => {
    const store = createPinsStore();
    store.getState().togglePin({ id: '1', name: 'Ubuntu Savers', balance: 500 } as never);
    expect(store.getState().pins).toEqual([{ id: '1', name: 'Ubuntu Savers' }]);
    store.getState().togglePin({ id: '1', name: 'Ubuntu Savers' });
    expect(store.getState().pins).toEqual([]);
  });

  it('forgets the pins when a different person claims the store', () => {
    const store = createPinsStore();
    store.getState().claim('lerato');
    store.getState().togglePin({ id: '1', name: 'A' });
    store.getState().claim('lerato');
    expect(store.getState().pins).toHaveLength(1); // same person keeps them
    store.getState().claim('sipho');
    expect(store.getState().pins).toEqual([]); // someone else: gone
  });

  it('prunes stokvels the member has left', () => {
    const store = createPinsStore();
    store.getState().togglePin({ id: '1', name: 'A' });
    store.getState().togglePin({ id: '2', name: 'B' });
    store.getState().prune(['2']);
    expect(store.getState().pins.map((p) => p.id)).toEqual(['2']);
  });

  it('clear empties everything', () => {
    const store = createPinsStore();
    store.getState().claim('lerato');
    store.getState().togglePin({ id: '1', name: 'A' });
    store.getState().clear();
    expect(store.getState().pins).toEqual([]);
    expect(store.getState().ownerId).toBeNull();
  });
});