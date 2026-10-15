#!/bin/bash
# One schema and one user per service (CLAUDE.md §6). Runs once, when the MySQL volume is first created.
# Sourced by the MySQL entrypoint when not executable, so no `set -u` here (it would leak into the entrypoint).
set -eo pipefail
mysql -uroot -p"${MYSQL_ROOT_PASSWORD}" <<SQL
CREATE DATABASE IF NOT EXISTS identity CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'identity_svc'@'%' IDENTIFIED BY '${IDENTITY_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON identity.* TO 'identity_svc'@'%';
CREATE DATABASE IF NOT EXISTS learner CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'learner_svc'@'%' IDENTIFIED BY '${LEARNER_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON learner.* TO 'learner_svc'@'%';
CREATE DATABASE IF NOT EXISTS cv CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'cv_svc'@'%' IDENTIFIED BY '${CV_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON cv.* TO 'cv_svc'@'%';
CREATE DATABASE IF NOT EXISTS gamification CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'gamification_svc'@'%' IDENTIFIED BY '${GAMIFICATION_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON gamification.* TO 'gamification_svc'@'%';
CREATE DATABASE IF NOT EXISTS assessment CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'assessment_svc'@'%' IDENTIFIED BY '${ASSESSMENT_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON assessment.* TO 'assessment_svc'@'%';
CREATE DATABASE IF NOT EXISTS learning CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'learning_svc'@'%' IDENTIFIED BY '${LEARNING_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON learning.* TO 'learning_svc'@'%';
CREATE DATABASE IF NOT EXISTS muni CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'muni_svc'@'%' IDENTIFIED BY '${MUNI_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON muni.* TO 'muni_svc'@'%';
CREATE DATABASE IF NOT EXISTS platform CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'platform_svc'@'%' IDENTIFIED BY '${PLATFORM_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON platform.* TO 'platform_svc'@'%';
CREATE DATABASE IF NOT EXISTS careers CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'careers_svc'@'%' IDENTIFIED BY '${CAREERS_DB_PASSWORD}';
GRANT ALL PRIVILEGES ON careers.* TO 'careers_svc'@'%';
FLUSH PRIVILEGES;
SQL
