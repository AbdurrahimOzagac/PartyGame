import unittest

import e2e_check

GOOD = ['@@READY', '@@STATE {"phase":"Lobby"}',
        '@@RESULT {"totals":[{"slot":0,"pid":"p1","bot":false,"total":7},{"slot":1,"pid":"p2","bot":false,"total":7},'
        '{"slot":2,"pid":"p3","bot":false,"total":4}],"rankings":[[{"slot":0,"rank":1},{"slot":1,"rank":2},{"slot":2,"rank":3}],'
        '[{"slot":1,"rank":1},{"slot":0,"rank":2},{"slot":2,"rank":3}]]}', '@@END']


class E2eCheckTests(unittest.TestCase):
    def test_good_run_passes(self):
        self.assertEqual(e2e_check.check(GOOD, 3, 2), [])

    def test_wrong_total_fails(self):
        bad = [l.replace('"total":4', '"total":5') for l in GOOD]
        self.assertTrue(any("slot 2 total 5" in e for e in e2e_check.check(bad, 3, 2)))

    def test_ties_share_the_higher_points(self):
        tie = ['@@READY', '@@RESULT {"totals":[{"slot":0,"pid":"a","bot":false,"total":4},{"slot":1,"pid":"b","bot":false,"total":3},'
               '{"slot":2,"pid":"c","bot":false,"total":3}],"rankings":[[{"slot":0,"rank":1},{"slot":1,"rank":2},{"slot":2,"rank":2}]]}', '@@END']
        self.assertEqual(e2e_check.check(tie, 3, 1), [])

    def test_missing_end_fails(self):
        self.assertIn("missing @@END", e2e_check.check(GOOD[:-1], 3, 2))


if __name__ == "__main__":
    unittest.main()
