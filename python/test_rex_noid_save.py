# Path: python/test_rex_noid_save.py
# Purpose: Verify /wp-json/rex/v1/save supports creation when no id is provided (post + page).
# Style: unittest (matches the rest of this directory)

import os
import base64
import json
import time
import unittest

import requests


class TestRexNoIdSave(unittest.TestCase):
    base = None
    sess = None
    created = []  # list[(kind: 'posts'|'pages', id:int)]

    @classmethod
    def setUpClass(cls):
        # Require env vars (same pattern as other tests)
        base = os.environ.get('WP_BASE_URL', '').rstrip('/')
        user = os.environ.get('WP_USERNAME')
        app  = os.environ.get('WP_APP_PASSWORD')

        if not base or not user or not app:
            raise unittest.SkipTest('WP_BASE_URL / WP_USERNAME / WP_APP_PASSWORD not set')

        cls.base = base

        # Basic auth session
        token = base64.b64encode(f'{user}:{app}'.encode('utf-8')).decode('ascii')
        s = requests.Session()
        s.headers.update({
            'Authorization': f'Basic {token}',
            'Accept': 'application/json',
            'Content-Type': 'application/json',
        })

        # Sanity ping
        r = s.get(f'{base}/wp-json/wp/v2', timeout=30)
        r.raise_for_status()

        cls.sess = s

    @classmethod
    def tearDownClass(cls):
        # Best-effort cleanup (force delete)
        for kind, pid in reversed(cls.created):
            try:
                cls.sess.delete(f'{cls.base}/wp-json/wp/v2/{kind}/{pid}?force=true', timeout=30)
            except Exception:
                pass

    # --- helpers -------------------------------------------------------------

    @staticmethod
    def _now_slug(prefix: str) -> str:
        return f'{prefix}-{int(time.time())}'

    @classmethod
    def _save_no_id(cls, data: dict, post_type: str = 'post') -> dict:
        url = f'{cls.base}/wp-json/rex/v1/save'
        payload = {
            # no "id" on purpose
            'post_type': post_type,  # 'post' or 'page' (or CPT)
            'data': data,
        }
        r = cls.sess.post(url, data=json.dumps(payload), timeout=30)
        r.raise_for_status()
        return r.json()

    @classmethod
    def _get_item(cls, pid: int, kind: str) -> dict:
        # kind: 'posts' or 'pages'
        url = f'{cls.base}/wp-json/wp/v2/{kind}/{pid}?context=edit&_fields=id,status,title,content,modified_gmt,meta'
        r = cls.sess.get(url, timeout=30)
        r.raise_for_status()
        return r.json()

    # --- tests ---------------------------------------------------------------

    def test_save_no_id_creates_post_and_ignores_original_meta(self):
        title = self._now_slug('NoId Title')
        body  = 'NoId body content'

        data = {
            'post_title': title,
            'post_content': body,
            'post_status': 'draft',
            # server must ignore this meta on create:
            'meta': { '_rex_original_post_id': 123456789 }
        }

        res = self._save_no_id(data, post_type='post')

        self.assertIn('id', res)
        self.assertIsInstance(res['id'], int)
        self.assertGreater(res['id'], 0)
        pid = res['id']
        self.created.append(('posts', pid))

        # saved flag and modified_gmt present; forked should be False/absent
        self.assertTrue(res.get('saved', True))
        self.assertIn(res.get('forked'), (False, None))
        self.assertIsInstance(res.get('modified_gmt'), str)

        # verify via core REST
        p = self._get_item(pid, 'posts')
        self.assertEqual('draft', p.get('status'))

        title_rendered = ((p.get('title') or {}).get('raw')
                          or (p.get('title') or {}).get('rendered') or '')
        content_rendered = ((p.get('content') or {}).get('raw')
                            or (p.get('content') or {}).get('rendered') or '')

        self.assertIn(title, title_rendered)
        self.assertIn(body, content_rendered)

        # _rex_original_post_id should be absent or cleared
        meta = p.get('meta') or {}
        if '_rex_original_post_id' in meta:
            v = meta['_rex_original_post_id']
            self.assertIn(v, (None, '', 0), msg=f'_rex_original_post_id should be cleared, got: {v}')

    def test_save_no_id_creates_page(self):
        title = self._now_slug('NoId Page')
        body  = 'NoId page body'

        res = self._save_no_id({
            'post_title': title,
            'post_content': body,
            'post_status': 'draft',
        }, post_type='page')

        self.assertIn('id', res)
        self.assertIsInstance(res['id'], int)
        self.assertGreater(res['id'], 0)
        page_id = res['id']
        self.created.append(('pages', page_id))

        p = self._get_item(page_id, 'pages')
        self.assertEqual('draft', p.get('status'))

        title_rendered = ((p.get('title') or {}).get('raw')
                          or (p.get('title') or {}).get('rendered') or '')
        content_rendered = ((p.get('content') or {}).get('raw')
                            or (p.get('content') or {}).get('rendered') or '')

        self.assertIn(title, title_rendered)
        self.assertIn(body, content_rendered)
