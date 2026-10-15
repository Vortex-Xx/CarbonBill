#!/usr/bin/env sh
set -e

# CarbonBill Monthly Backup & Restore Drill
# Executes a full cycle test: Backup -> Mutate -> Restore -> Integrity Verification

echo "=== Starting CarbonBill Disaster Recovery Drill ==="

DATA_DIR="/tmp/carbonbill_drill"
mkdir -p "${DATA_DIR}/backups"

DB_FILE="${DATA_DIR}/carbonbill.db"

# Initialize test database
sqlite3 "${DB_FILE}" "CREATE TABLE drill_test (id INTEGER PRIMARY KEY, value TEXT); INSERT INTO drill_test (value) VALUES ('original_data');"

# Run backup
DATA_DIR="${DATA_DIR}" ./infra/backup.sh

# Mutate active database
sqlite3 "${DB_FILE}" "INSERT INTO drill_test (value) VALUES ('corrupted_data');"
COUNT_BEFORE=$(sqlite3 "${DB_FILE}" "SELECT count(*) FROM drill_test;")
echo "Row count after mutation: ${COUNT_BEFORE}"

# Find the latest backup
LATEST_BACKUP=$(ls -t "${DATA_DIR}/backups"/carbonbill_*.db | head -n 1)

# Run restore
DATA_DIR="${DATA_DIR}" ./infra/restore.sh "${LATEST_BACKUP}"

# Verify restoration restored original state
COUNT_AFTER=$(sqlite3 "${DB_FILE}" "SELECT count(*) FROM drill_test;")
echo "Row count after restore: ${COUNT_AFTER}"

if [ "${COUNT_AFTER}" -ne 1 ]; then
    echo "DRILL FAILED: Expected 1 row after restore, found ${COUNT_AFTER}"
    rm -rf "${DATA_DIR}"
    exit 1
fi

echo "=== CarbonBill Disaster Recovery Drill Passed Successfully ==="
rm -rf "${DATA_DIR}"
