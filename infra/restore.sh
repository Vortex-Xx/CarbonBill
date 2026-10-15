#!/usr/bin/env sh
set -e

# CarbonBill SQLite Database Restore Script
# Restores a verified backup into the active database directory

DATA_DIR="${DATA_DIR:-/data}"
DB_FILE="${DATA_DIR}/carbonbill.db"
BACKUP_FILE="$1"

if [ -z "${BACKUP_FILE}" ]; then
    echo "Usage: $0 <path_to_backup_file.db>"
    exit 1
fi

if [ ! -f "${BACKUP_FILE}" ]; then
    echo "ERROR: Backup file ${BACKUP_FILE} not found."
    exit 1
fi

# Verify SHA-256 checksum if exists
if [ -f "${BACKUP_FILE}.sha256" ]; then
    echo "Verifying SHA-256 checksum..."
    sha256sum -c "${BACKUP_FILE}.sha256"
fi

# Verify database integrity before restoring
echo "Verifying SQLite integrity of backup..."
INTEGRITY=$(sqlite3 "${BACKUP_FILE}" "PRAGMA integrity_check;")
if [ "${INTEGRITY}" != "ok" ]; then
    echo "ERROR: Corrupted backup file: ${INTEGRITY}"
    exit 1
fi

# Backup existing database before replacement
if [ -f "${DB_FILE}" ]; then
    echo "Safeguarding current active database to ${DB_FILE}.pre_restore..."
    cp "${DB_FILE}" "${DB_FILE}.pre_restore"
fi

# Atomic restore
echo "Restoring ${BACKUP_FILE} to ${DB_FILE}..."
cp "${BACKUP_FILE}" "${DB_FILE}"

# Re-enable WAL mode and busy timeout
sqlite3 "${DB_FILE}" "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;"

echo "Database restored successfully and WAL journal mode re-enabled."
