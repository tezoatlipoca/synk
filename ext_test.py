# -*- coding: utf-8 -*-
# This script is designed to test any synk instance by generating random files,
# uploading them to the synk server, and then downloading them back to verify
# integrity. It also handles the case where the total size of files exceeds
# the maximum store size allowed by the synk server.

# You should see at least one, possibly more HTTP 413 errors, depending
# on the size of the files generated and the max store size set on your
# synk instance.. and if your instance already has files in its blob store

import os
import random
import string
import requests
from pathlib import Path
import shutil

SYNK_URL = "https://synk.tezoatlipoca.com"  # Change to your synk instance URL aka --hostname
MAX_STORE_SIZE = 10 * 1024 * 1024   # set this to how big --maxsynkstoresize is on your instance

TMP_DIR = Path("synk_test_tmp")

def clear_tmp_dir():
    if TMP_DIR.exists():
        shutil.rmtree(TMP_DIR)
    TMP_DIR.mkdir(exist_ok=True)

def random_filename():
    return ''.join(random.choices(string.ascii_lowercase + string.digits, k=12))

def random_file_size():
    return random.randint(30 * 1024, int(1.5 * 1024 * 1024))

def generate_random_file(path, size):
    with open(path, "wb") as f:
        f.write(os.urandom(size))

def get_synk_key():
    resp = requests.get(f"{SYNK_URL}/key")
    resp.raise_for_status()
    return resp.text.strip().splitlines()[-1]

def upload_file(key, path):
    with open(path, "rb") as f:
        resp = requests.put(f"{SYNK_URL}/blob/{key}", data=f)
        return resp.status_code

def download_file(key, path):
    resp = requests.get(f"{SYNK_URL}/blob/{key}")
    print(f"Download /blob/{key}: HTTP {resp.status_code}")
    resp.raise_for_status()
    with open(path, "wb") as f:
        f.write(resp.content)

def pretty_size(num_bytes):
    for unit in ['B', 'KB', 'MB', 'GB']:
        if num_bytes < 1024.0:
            return f"{num_bytes:3.1f}{unit}"
        num_bytes /= 1024.0
    return f"{num_bytes:.1f}TB"

def main():
    clear_tmp_dir()
    print("Generating random files and keys...")

    # 1. Generate a random number of files (between 10 and 30 for variety)
    num_files = random.randint(10, 30)
    file_info = []
    total_size = 0

    # 2. Generate enough files to exceed MAX_STORE_SIZE
    while total_size <= MAX_STORE_SIZE or len(file_info) < num_files:
        fname = random_filename()
        fpath = TMP_DIR / fname
        fsize = random_file_size()
        generate_random_file(fpath, fsize)
        key = get_synk_key()
        file_info.append({"key": key, "path": fpath, "size": fsize})
        total_size += fsize

    print(f"Generated {len(file_info)} files, total size: {total_size / (1024*1024):.2f} MB")

    # 3. Upload files, track which succeed
    uploaded_files = []
    failed_files = []
    for info in file_info:
        status = upload_file(info["key"], info["path"])
        size_str = pretty_size(info["size"])
        if status == 200:
            uploaded_files.append(info)
            result = "OK"
        else:
            failed_files.append(info)
            result = f"FAILED (HTTP {status})"
        print(f"Upload {info['path']} -> /blob/{info['key']} ({size_str}): {result}")

    print(f"\n{len(uploaded_files)} files uploaded successfully, {len(failed_files)} failed due to size restriction or other errors.")

    # 4. Download only successfully uploaded files in random order and verify
    print("Downloading files in random order and verifying...")
    download_order = uploaded_files[:]
    random.shuffle(download_order)
    for info in download_order:
        download_path = TMP_DIR / f"dl_{info['path'].name}"
        download_file(info["key"], download_path)
        with open(info["path"], "rb") as orig, open(download_path, "rb") as dl:
            orig_bytes = orig.read()
            dl_bytes = dl.read()
            if orig_bytes != dl_bytes:
                print(f"ERROR: Mismatch for key {info['key']}")
            else:
                print(f"Verified {info['key']} OK")

    print("Test complete.")

if __name__ == "__main__":
    main()