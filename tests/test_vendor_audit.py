import importlib.util,pathlib,tempfile,json,hashlib,unittest
p=pathlib.Path(__file__).resolve().parents[1]/'.github/scripts/verify-cargo-vendor.py';s=importlib.util.spec_from_file_location('vendor_audit',p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
class Tests(unittest.TestCase):
 def fixture(self,root):
  d=root/'vendor'/'demo';d.mkdir(parents=True);(d/'Cargo.toml').write_text('[package]\nname="demo"\nversion="1.0.0"\nlicense="MIT"\n');(d/'src.txt').write_text('original');sums={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in d.iterdir()};(d/'.cargo-checksum.json').write_text(json.dumps({'package':'a'*64,'files':sums}));lock=root/'Cargo.lock';lock.write_text('[[package]]\nname="demo"\nversion="1.0.0"\nsource="registry+https://example.invalid/index"\nchecksum="'+'a'*64+'"\n');return lock,root/'vendor',d
 def test_valid(self):
  with tempfile.TemporaryDirectory() as t:
   l,v,d=self.fixture(pathlib.Path(t));r=m.verify(l,v);self.assertEqual(r['missing']+r['corrupt']+r['unsupportedSources'],[])
 def test_tamper(self):
  with tempfile.TemporaryDirectory() as t:
   l,v,d=self.fixture(pathlib.Path(t));(d/'src.txt').write_text('changed');self.assertTrue(m.verify(l,v)['corrupt'])
 def test_missing(self):
  with tempfile.TemporaryDirectory() as t:
   l,v,d=self.fixture(pathlib.Path(t));(d/'.cargo-checksum.json').unlink();self.assertTrue(m.verify(l,v)['missing'])
 def test_escape(self):
  with tempfile.TemporaryDirectory() as t:
   l,v,d=self.fixture(pathlib.Path(t));c=d/'.cargo-checksum.json';r=json.loads(c.read_text());r['files']['../outside']='a'*64;c.write_text(json.dumps(r));self.assertIn('outside crate',m.verify(l,v)['corrupt'][0]['errors'][0])
 def test_git_source_not_silently_accepted(self):
  with tempfile.TemporaryDirectory() as t:
   l,v,d=self.fixture(pathlib.Path(t));l.write_text(l.read_text().replace('registry+https://example.invalid/index','git+https://example.invalid/repo#1234'));self.assertTrue(m.verify(l,v)['unsupportedSources'])
unittest.main()
