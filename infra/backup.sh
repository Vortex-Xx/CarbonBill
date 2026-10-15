#!/usr/bin/env sh
set -e

# CarbonBill SQLite Atomic Backup Script
# Performs VACUUM INTO atomic snapshot, computes SHA-256, and rotates old backups

DATA_DIR="${DATA_DIR:-/data}"
BACKUP_DIR="${DATA_DIR}/backups"
TIMESTAMP=$(date -u +"%Y%m%d_%H%M%S")
DB_FILE="${DATA_DIR}/carbonbill.db"
BACKUP_FILE="${BACKUP_DIR}/carbonbill_${TIMESTAMP}.db"

mkdir -p "${BACKUP_DIR}"

if [ ! -f "${DB_FILE}" ]; then
    echo "Database file ${DB_FILE} does not exist. Skipping backup."
    exit 0
fi

echo "Starting atomic backup of ${DB_FILE} -> ${BACKUP_FILE}..."

# Execute SQLite atomic online backup using VACUUM INTO
sqlite3 "${DB_FILE}" "VACUUM INTO '${BACKUP_FILE}';"

# Verify integrity of backup
INTEGRITY=$(sqlite3 "${BACKUP_FILE}" "PRAGMA integrity_check;")
if [ "${INTEGRITY}" != "ok" ]; then
    echo "ERROR: Backup integrity check failed: ${INTEGRITY}"
    rm -f "${BACKUP_FILE}"
    exit 1
fi

# Compute SHA-256 checksum
sha256sum "${BACKUP_FILE}" > "${BACKUP_FILE}.sha256"
echo "Backup created successfully with SHA-256 checksum: $(cat "${BACKUP_FILE}.sha256")"

# Rotate backups older than 30 days
find "${BACKUP_DIR}" -name "carbonbill_*.db*" -mtime +30 -exec rm -f {} \;
echo "Backup rotation completed (retention: 30 days)."
