import argparse, hashlib, json, zipfile
from pathlib import Path

def digest(stream):
    h=hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024*1024),b''):
        h.update(chunk)
    return h.hexdigest().upper()

parser=argparse.ArgumentParser()
parser.add_argument('unsigned',type=Path)
parser.add_argument('signed',type=Path)
parser.add_argument('report',type=Path)
args=parser.parse_args()
with zipfile.ZipFile(args.unsigned) as source, zipfile.ZipFile(args.signed) as test:
    source_names=set(source.namelist()); test_names=set(test.namelist())
    changed=[]
    for name in sorted(source_names & test_names):
        with source.open(name) as a, test.open(name) as b:
            if digest(a)!=digest(b): changed.append(name)
    added=sorted(test_names-source_names); removed=sorted(source_names-test_names)
    ok=not removed and set(changed)<= {'[Content_Types].xml'} and set(added)<= {'AppxSignature.p7x','AppxMetadata/CodeIntegrity.cat'} and 'AppxSignature.p7x' in added
with args.unsigned.open('rb') as a,args.signed.open('rb') as b:
    report={'unsigned':str(args.unsigned),'unsigned_sha256':digest(a),'signed':str(args.signed),'signed_sha256':digest(b),'common_entries':len(source_names & test_names),'changed_common_entries':changed,'signed_only_entries':added,'removed_entries':removed,'payload_equivalence_pass':ok}
args.report.write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
raise SystemExit(0 if ok else 1)
