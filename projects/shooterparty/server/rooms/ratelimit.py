"""Sliding-window counters per (bucket, address), with an optional lock-out once a limit is hit."""
import collections
import threading

MAX_KEYS = 10000   # bounded memory: the oldest addresses are forgotten first


class Limiter:
    def __init__(self, clock, limit, window, block=0.0):
        self.clock, self.limit, self.window, self.block = clock, limit, window, block
        self.hits = collections.OrderedDict()   # key -> deque of times
        self.blocked = {}                       # key -> until
        self.lock = threading.Lock()

    def blocked_now(self, key):
        with self.lock:
            until = self.blocked.get(key)
            if until and self.clock() < until:
                return True
            self.blocked.pop(key, None)
            return False

    def hit(self, key):
        """Counts one event; returns False when the key is over its limit (and starts the lock-out)."""
        now = self.clock()
        with self.lock:
            until = self.blocked.get(key)
            if until and now < until:
                return False
            q = self.hits.pop(key, None) or collections.deque()
            while q and now - q[0] >= self.window:
                q.popleft()
            self.hits[key] = q
            while len(self.hits) > MAX_KEYS:
                self.hits.popitem(last=False)
            if len(q) >= self.limit:   # a refused attempt is not counted, so retrying does not extend the wait
                if self.block:
                    self.blocked[key] = now + self.block
                return False
            q.append(now)
            return True
