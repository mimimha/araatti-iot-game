-- 아라아띠 DB 덤프 (MySQL 8.4) — 2026-09-28 01:33 UTC 운영 DB 기준
-- ⚠ 개인정보를 가렸다: users.email = user{id}@example.com, users.password_hash = 시연용 공통 비밀번호 'araatti1234!' 의 bcrypt 해시
-- 복원: mysql -u <계정> -p araatti < 03_araatti_dump.sql   (DB araatti 를 먼저 만들어 둔다)
SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS=0;

-- users (구조)

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;
DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `email` varchar(190) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `password_hash` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `last_login_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_users_email` (`email`)
) ENGINE=InnoDB AUTO_INCREMENT=45 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- users (가린 데이터)
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (1,'user1@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-21 14:17:51.744564','2026-09-28 01:18:53.302588');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (2,'user2@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-21 15:03:14.467647','2026-09-27 10:54:55.277752');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (3,'user3@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-21 15:31:20.855887','2026-09-28 00:27:31.216638');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (4,'user4@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-21 15:36:41.801927','2026-09-23 17:49:00.573014');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (5,'user5@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-21 15:42:40.351323','2026-09-21 15:44:59.988950');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (6,'user6@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-21 15:51:35.140837','2026-09-21 16:00:05.123523');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (7,'user7@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-22 01:10:11.164994','2026-09-22 01:14:31.974204');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (8,'user8@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-22 05:33:32.960254','2026-09-28 00:29:05.546485');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (9,'user9@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-22 05:58:52.074175','2026-09-22 06:11:24.335428');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (10,'user10@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-22 06:13:46.430595','2026-09-22 06:22:53.885054');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (11,'user11@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-22 06:14:24.228033','2026-09-22 06:14:24.666084');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (12,'user12@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-22 06:18:30.192858','2026-09-22 06:24:41.750698');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (13,'user13@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-23 04:54:38.634786','2026-09-23 04:54:39.123651');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (14,'user14@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-23 08:02:32.942039','2026-09-23 08:02:33.378946');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (15,'user15@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-24 11:58:00.273701','2026-09-24 11:58:01.123108');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (16,'user16@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-24 12:13:41.996688','2026-09-24 12:13:42.418023');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (17,'user17@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-24 12:19:48.841568','2026-09-24 12:19:49.235371');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (18,'user18@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-24 12:20:31.055472','2026-09-24 12:20:31.464925');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (19,'user19@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-24 12:26:05.264984','2026-09-28 00:00:36.028270');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (20,'user20@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-24 21:37:27.816714','2026-09-24 22:14:13.383258');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (21,'user21@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-24 21:38:09.923757','2026-09-24 22:14:34.868874');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (22,'user22@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-24 21:49:20.260254','2026-09-24 21:49:20.656710');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (23,'user23@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-25 07:16:25.238000','2026-09-25 07:16:26.086649');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (24,'user24@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-25 12:10:11.043658','2026-09-25 12:10:11.526157');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (25,'user25@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-25 12:11:14.201973','2026-09-25 12:11:14.610870');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (26,'user26@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-25 13:08:08.517604','2026-09-25 13:08:08.912604');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (27,'user27@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-25 13:08:53.757654','2026-09-25 13:08:54.150761');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (28,'user28@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-25 19:47:47.402125','2026-09-28 00:25:59.641818');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (29,'user29@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-25 19:51:36.014639','2026-09-27 21:04:46.846974');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (30,'user30@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-26 11:52:31.080461','2026-09-27 14:46:53.114886');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (31,'user31@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-26 12:03:26.008511','2026-09-26 12:03:26.415148');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (32,'user32@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 12:15:17.137246','2026-09-27 12:15:18.230045');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (33,'user33@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 12:26:16.335267','2026-09-27 12:26:16.771200');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (34,'user34@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 12:30:31.092945','2026-09-27 12:30:31.507270');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (35,'user35@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 12:56:41.760261','2026-09-27 12:56:42.271702');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (36,'user36@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 13:11:05.318660','2026-09-27 13:11:05.740510');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (37,'user37@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 14:00:29.622086','2026-09-27 23:52:18.424333');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (38,'user38@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 14:13:12.947370','2026-09-27 21:00:53.504598');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (39,'user39@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 14:45:49.309811','2026-09-27 14:57:56.451681');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (40,'user40@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 18:32:22.255657','2026-09-27 18:32:22.733967');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (41,'user41@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 20:34:19.852622','2026-09-27 20:34:20.260935');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (42,'user42@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-27 23:53:18.517223','2026-09-27 23:53:19.058747');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (43,'user43@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-28 00:09:18.530875','2026-09-28 00:09:18.945910');
INSERT INTO `users` (`id`,`email`,`password_hash`,`created_at`,`last_login_at`) VALUES (44,'user44@example.com','$2a$12$3ktUyCPyzJ5Y4xvlJfEIneb7BNLfEZR4vRtnaqi5fA5rf8DmQvYsq','2026-09-28 00:31:21.407701','2026-09-28 00:31:21.920217');

-- 나머지 표 (구조 + 데이터 그대로)

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;
DROP TABLE IF EXISTS `__EFMigrationsHistory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `__EFMigrationsHistory` (
  `MigrationId` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ProductVersion` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

LOCK TABLES `__EFMigrationsHistory` WRITE;
/*!40000 ALTER TABLE `__EFMigrationsHistory` DISABLE KEYS */;
INSERT INTO `__EFMigrationsHistory` VALUES ('20260908075724_InitialCreate','9.0.19'),('20260920051333_AddInventoryAndAltar','9.0.19');
/*!40000 ALTER TABLE `__EFMigrationsHistory` ENABLE KEYS */;
UNLOCK TABLES;
DROP TABLE IF EXISTS `altar_contributions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `altar_contributions` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `request_id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
  `user_id` bigint unsigned NOT NULL,
  `amount` int unsigned NOT NULL,
  `created_at` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_contributions_user_request` (`user_id`,`request_id`),
  KEY `idx_contributions_user` (`user_id`),
  CONSTRAINT `fk_contributions_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=36 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

LOCK TABLES `altar_contributions` WRITE;
/*!40000 ALTER TABLE `altar_contributions` DISABLE KEYS */;
INSERT INTO `altar_contributions` VALUES (1,'a1dba7a3-f17b-4b38-bef4-2631ba78b1d1',1,1,'2026-09-25 08:58:46.498413'),(2,'0a1b6b33-8a75-4318-8d78-6d1cf63fcb9a',1,1,'2026-09-25 09:09:49.854453'),(3,'f6fa70aa-6258-4a2d-bfeb-e07510199f10',1,1,'2026-09-25 09:38:57.800887'),(4,'7a696893-f303-4445-a578-4e7f0af4e130',25,2,'2026-09-25 12:56:03.511810'),(5,'1e5a82b5-fae3-4181-9dc8-78b4d379b501',3,2,'2026-09-25 12:56:37.847977'),(6,'d963f1d7-7ad9-4e81-8c96-3457f172c2c0',19,1,'2026-09-25 12:56:38.547270'),(7,'12f00f6c-6c97-4103-a065-8a842e128164',19,1,'2026-09-25 12:56:39.997992'),(8,'834c53d7-3d45-4dc1-8bfb-dca101cda145',28,3,'2026-09-26 12:24:25.460640'),(9,'681b9bc8-9fe3-4a38-ae92-1e82020359eb',31,1,'2026-09-26 12:25:15.500868'),(10,'6adbecc9-74ef-4511-b946-892d52820699',31,1,'2026-09-26 12:25:19.084089'),(11,'f5b8054c-3ef1-4176-80e0-554887212376',1,1,'2026-09-27 05:01:15.812116'),(12,'2d3f7dd6-5d00-4929-b74e-877ba134f5a3',1,1,'2026-09-27 05:01:23.856137'),(13,'a0893c2d-d870-4bb4-ac03-a4b14fb5bb63',1,1,'2026-09-27 05:01:38.887517'),(14,'dbc04622-4375-406a-92b7-cd163d7e48f0',1,1,'2026-09-27 05:18:59.175629'),(15,'f50f0a07-a378-4cf6-a4d4-c1d1df1c353f',1,1,'2026-09-27 05:19:04.998855'),(16,'d1deec4a-2185-4641-a31c-bffae9437fab',1,1,'2026-09-27 05:31:24.620117'),(17,'7a3b2229-a8f6-414c-9526-00d49137a9cc',1,1,'2026-09-27 05:32:49.046427'),(18,'0855c9c8-11a4-4ce5-aaec-e3bf1741b1b2',1,1,'2026-09-27 05:32:55.415397'),(19,'3507f27f-e23f-4d27-9c1a-fd14caa59e29',1,1,'2026-09-27 05:44:51.138351'),(20,'ff537234-f8a0-4c5c-b35a-b9e1e1d418b0',1,1,'2026-09-27 05:44:56.519949'),(21,'51f6cd77-e131-4794-9f5d-b043fd662b8b',1,1,'2026-09-27 10:51:20.607758'),(22,'231780b4-249c-4905-91bb-9a7f890403e5',19,2,'2026-09-27 12:18:41.372829'),(23,'ca44e5f0-78f0-4a6a-804b-db6290b6c62b',19,1,'2026-09-27 12:46:43.892117'),(24,'261067fd-a0ac-440c-a636-1d1bf3a28675',19,1,'2026-09-27 12:46:56.184426'),(25,'b09ffa60-1ddb-45b4-ae32-dcc5ac3ed613',1,1,'2026-09-27 12:46:58.937606'),(26,'6925a706-381d-4203-bbdb-89c8d6fc653c',1,1,'2026-09-27 12:47:10.304171'),(27,'d5002bc0-076c-49ca-874b-16fbdf452e95',35,3,'2026-09-27 13:06:06.918659'),(28,'c77f7545-b153-4a8e-a6ba-53dc887f741c',19,1,'2026-09-27 13:06:14.848588'),(29,'f9bd0ad4-339e-4f31-8cd8-a2ddbddc98ec',36,2,'2026-09-27 13:17:30.830408'),(30,'b7b44509-d632-4ab2-a206-a6d7d88fcc1b',19,1,'2026-09-27 13:17:35.583699'),(31,'b847d692-4197-4a75-bea4-441ae3f8f2af',34,1,'2026-09-27 13:17:40.759413'),(32,'18e60552-ffc5-4eb8-aed3-c6195bb1db48',39,1,'2026-09-27 14:59:24.644115'),(33,'b194b6a4-0d64-4839-811c-ea9446b230d9',39,2,'2026-09-27 15:15:51.583167'),(34,'aa899208-035a-4d18-87fa-978ba8ea4da5',19,4,'2026-09-27 15:16:32.096711'),(35,'27eabb81-9be9-4313-9433-25940a539c1f',44,2,'2026-09-28 00:39:40.840877');
/*!40000 ALTER TABLE `altar_contributions` ENABLE KEYS */;
UNLOCK TABLES;
DROP TABLE IF EXISTS `altar_state`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `altar_state` (
  `id` int NOT NULL,
  `total_offered` bigint unsigned NOT NULL DEFAULT '0',
  `target_offering` int unsigned NOT NULL DEFAULT '1000',
  `activated_at` datetime(6) DEFAULT NULL,
  `updated_at` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  CONSTRAINT `ck_altar_target_positive` CHECK ((`target_offering` > 0)),
  CONSTRAINT `ck_altar_total_le_target` CHECK ((`total_offered` <= `target_offering`)),
  CONSTRAINT `ck_altar_total_nonneg` CHECK ((`total_offered` >= 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

LOCK TABLES `altar_state` WRITE;
/*!40000 ALTER TABLE `altar_state` DISABLE KEYS */;
INSERT INTO `altar_state` VALUES (1,2,100,NULL,'2026-09-28 00:39:40.833825');
/*!40000 ALTER TABLE `altar_state` ENABLE KEYS */;
UNLOCK TABLES;
DROP TABLE IF EXISTS `character_parts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_parts` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `character_id` bigint unsigned NOT NULL,
  `slot` varchar(24) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `prefab_name` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_parts_character_slot` (`character_id`,`slot`),
  CONSTRAINT `fk_parts_character` FOREIGN KEY (`character_id`) REFERENCES `characters` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=193 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

LOCK TABLES `character_parts` WRITE;
/*!40000 ALTER TABLE `character_parts` DISABLE KEYS */;
INSERT INTO `character_parts` VALUES (1,1,'Face','Female_Emotion_Tongue_01'),(2,1,'Top','Outfit_07'),(3,1,'Accessory','Hat_40'),(4,2,'Face','Male_Emotion_Happy_01'),(5,2,'Top','Outwear_03'),(6,2,'Bottom','Costume_3_04'),(7,2,'Accessory','Glasses_01'),(8,3,'Face','Male_Emotion_Surpriced_01'),(9,3,'Shoes','Shoe_Slippers_01'),(10,3,'Top','Outwear_56'),(11,3,'Bottom','Pants_04'),(12,3,'Accessory','Costume_14_02'),(13,4,'Face','Female_Emotion_Sad_02'),(14,4,'Shoes','Shoe_Slippers_03'),(15,4,'Top','Outwear_35'),(16,4,'Bottom','Shorts_01'),(17,4,'Accessory','Costume_2_02'),(18,5,'Face','Female_Emotion_Confuse_01'),(19,5,'Top','Outwear_02'),(20,5,'Bottom','Costume_14_03'),(21,5,'Accessory','Hat_50'),(22,6,'Face','Male_Emotion_Tongue_01'),(23,6,'Shoes','Shoe_Slippers_03'),(24,6,'Top','Outwear_30'),(25,6,'Bottom','Shorts_01'),(26,6,'Accessory','Hat_27'),(27,7,'Face','Female_Emotion_Anger_01'),(28,7,'Shoes','Shoe_Sneakers_07'),(29,7,'Top','Outwear_19'),(30,7,'Bottom','Pants_04'),(31,7,'Accessory','Hat_13'),(32,8,'Face','Female_Emotion_Cry_01'),(33,8,'Top','Outwear_43'),(34,8,'Bottom','Costume_11_05'),(35,8,'Accessory','Hat_44'),(36,9,'Face','Male_Emotion_Surpriced_02'),(37,9,'Shoes','Shoe_Slippers_01'),(38,9,'Top','Outwear_08'),(39,9,'Bottom','Pants_15'),(40,9,'Accessory','Hat_24'),(41,10,'Face','Male_Emotion_Surpriced_01'),(42,10,'Hair','Hairstyle_Female_07'),(43,10,'Top','Outfit_07'),(44,10,'Accessory','Glasses_01'),(45,11,'Face','Female_Emotion_Angry_01'),(46,11,'Hair','Hairstyle_Male_06'),(47,11,'Shoes','Shoe_Slippers_01'),(48,11,'Top','Outwear_38'),(49,11,'Bottom','Pants_07'),(50,11,'Accessory','Glasses_19'),(51,12,'Face','Female_Emotion_Happy_01'),(52,12,'Shoes','Shoe_Slippers_01'),(53,12,'Top','Outwear_01'),(54,12,'Bottom','Pants_01'),(55,12,'Accessory','Costume_14_02'),(56,13,'Face','Male_Emotion_Usual_01'),(57,13,'Top','Costume_14_01'),(58,13,'Bottom','Costume_14_03'),(59,13,'Accessory','Costume_14_02'),(60,14,'Face','Female_Emotion_Evil_02'),(61,14,'Top','Costume_14_01'),(62,14,'Bottom','Costume_14_03'),(63,14,'Accessory','Costume_14_02'),(64,15,'Top','Costume_14_01'),(65,15,'Bottom','Costume_14_03'),(66,15,'Accessory','Costume_14_02'),(67,16,'Face','Male_Emotion_Usual_01'),(68,16,'Top','Costume_14_01'),(69,16,'Bottom','Costume_14_03'),(70,16,'Accessory','Costume_14_02'),(71,17,'Face','Male_Emotion_Usual_01'),(72,17,'Top','Costume_14_01'),(73,17,'Bottom','Costume_14_03'),(74,17,'Accessory','Costume_14_02'),(75,18,'Face','Female_Emotion_Anger_01'),(76,18,'Shoes','Shoe_Sneakers_15'),(77,18,'Top','Outwear_43'),(78,18,'Bottom','Pants_08'),(79,18,'Accessory','Mustache_15'),(80,19,'Face','Male_Emotion_Surpriced_01'),(81,19,'Hair','Hairstyle_Male_11'),(82,19,'Top','Outfit_08'),(83,20,'Face','Female_Emotion_Angry_02'),(84,20,'Top','Costume_14_01'),(85,20,'Bottom','Costume_14_03'),(86,20,'Accessory','Costume_14_02'),(87,21,'Face','Male_Emotion_Usual_01'),(88,21,'Top','Outwear_16'),(89,21,'Bottom','Pants_10'),(90,21,'Accessory','Hat_12'),(91,22,'Face','Female_Emotion_Anger_01'),(92,22,'Top','Costume_14_01'),(93,22,'Bottom','Costume_14_03'),(94,22,'Accessory','Costume_14_02'),(95,23,'Face','Male_Emotion_Usual_02'),(96,23,'Hair','Hairstyle_Male_12'),(97,23,'Shoes','Shoe_Sneakers_01'),(98,23,'Top','Outwear_06'),(99,23,'Bottom','Pants_01'),(100,23,'Glasses','Bandage_01'),(101,23,'FaceAccessory','Mask_02'),(102,24,'Face','Male_Emotion_Usual_01'),(103,24,'Top','Costume_14_01'),(104,24,'Bottom','Costume_14_03'),(105,24,'Hat','Costume_14_02'),(106,25,'Face','Female_Emotion_Cry_01'),(107,25,'Shoes','Shoe_Slippers_05'),(108,25,'Top','Outwear_43'),(109,25,'Bottom','Costume_11_05'),(110,25,'Hat','Hat_44'),(111,26,'Face','Male_Emotion_Surpriced_02'),(112,26,'Hair','Hairstyle_Female_11'),(113,26,'Shoes','Shoe_Slippers_05'),(114,26,'Bottom','Shorts_02'),(115,27,'Face','Male_Emotion_Tongue_01'),(116,27,'Hair','Hairstyle_Male_07'),(117,27,'Shoes','Costume_9_05'),(118,27,'Top','Outwear_04'),(119,27,'Bottom','Pants_01'),(120,27,'Glasses','Glasses_11'),(121,28,'Face','Male_Emotion_Tongue_01'),(122,28,'Hair','Hairstyle_Male_07'),(123,28,'Shoes','Costume_9_05'),(124,28,'Top','Outwear_04'),(125,28,'Bottom','Pants_01'),(126,28,'Glasses','Glasses_11'),(127,29,'Face','Male_Emotion_Neutral_01'),(128,29,'Hair','Hairstyle_Female_06'),(129,29,'Shoes','Shoe_Sneakers_02'),(130,29,'Top','Outwear_31'),(131,29,'Bottom','Costume_3_04'),(132,29,'Glasses','Glasses_07'),(133,30,'Face','Male_Emotion_Usual_01'),(134,30,'Top','Costume_14_01'),(135,30,'Bottom','Costume_14_03'),(136,30,'Hat','Costume_14_02'),(137,31,'Face','Male_Emotion_Usual_01'),(138,31,'Hair','Hairstyle_Male_11'),(139,31,'Shoes','Shoe_Slippers_01'),(140,31,'Top','Outwear_51'),(141,31,'Bottom','Pants_07'),(142,32,'Face','Female_Emotion_Sad_01'),(143,32,'Hair','Hairstyle_Male_11'),(144,32,'Shoes','Shoe_Slippers_01'),(145,32,'Top','Outwear_51'),(146,32,'Bottom','Pants_07'),(147,33,'Face','Female_Emotion_Happy_01'),(148,33,'Hair','Hairstyle_Female_02'),(149,33,'Shoes','Costume_5_04'),(150,33,'Top','Outwear_02'),(151,33,'Bottom','Pants_02'),(152,33,'Glasses','Glasses_12'),(153,34,'Face','Female_Emotion_Anger_01'),(154,34,'Hair','Hairstyle_Female_03'),(155,34,'Shoes','Shoe_Sneakers_02'),(156,34,'Top','Outwear_02'),(157,34,'Bottom','Pants_08'),(158,34,'Glasses','Glasses_01'),(159,35,'Face','Female_Emotion_Happy_01'),(160,35,'Top','Costume_14_01'),(161,35,'Bottom','Costume_14_03'),(162,35,'Hat','Costume_14_02'),(163,36,'Face','Male_Emotion_Usual_01'),(164,36,'Top','Costume_14_01'),(165,36,'Bottom','Costume_14_03'),(166,36,'Hat','Costume_14_02'),(167,37,'Face','Male_Emotion_Usual_01'),(168,37,'Top','Costume_14_01'),(169,37,'Bottom','Costume_14_03'),(170,37,'Hat','Costume_14_02'),(171,38,'Face','Female_Emotion_Evil_01'),(172,38,'Top','Costume_14_01'),(173,38,'Bottom','Costume_14_03'),(174,38,'Hat','Costume_14_02'),(175,39,'Face','Male_Emotion_Usual_01'),(176,39,'Top','Costume_14_01'),(177,39,'Bottom','Costume_14_03'),(178,39,'Hat','Costume_14_02'),(179,40,'Face','Male_Emotion_Usual_01'),(180,40,'Top','Costume_14_01'),(181,40,'Bottom','Costume_14_03'),(182,40,'Hat','Costume_14_02'),(183,41,'Face','Male_Emotion_Happy_01'),(184,41,'Hair','Hairstyle_Male_11'),(185,41,'Shoes','Shoe_Slippers_01'),(186,41,'Top','Outwear_51'),(187,41,'Bottom','Pants_07'),(188,42,'Face','Female_Emotion_Surpriced_02'),(189,42,'Hair','Hairstyle_Female_02'),(190,42,'Top','Costume_14_01'),(191,42,'Bottom','Costume_14_03'),(192,42,'Glasses','Glasses_07');
/*!40000 ALTER TABLE `character_parts` ENABLE KEYS */;
UNLOCK TABLES;
DROP TABLE IF EXISTS `characters`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `characters` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned NOT NULL,
  `name` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `skin_color` char(7) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `slot_index` tinyint unsigned NOT NULL DEFAULT '0',
  `created_at` datetime(6) NOT NULL,
  `updated_at` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_characters_name` (`name`),
  UNIQUE KEY `uk_characters_user_slot` (`user_id`,`slot_index`),
  KEY `idx_characters_user_id` (`user_id`),
  CONSTRAINT `fk_characters_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=43 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

LOCK TABLES `characters` WRITE;
/*!40000 ALTER TABLE `characters` DISABLE KEYS */;
INSERT INTO `characters` VALUES (1,1,'relu','#F5C2A3',0,'2026-09-21 14:19:38.085832','2026-09-21 14:19:38.085832'),(2,2,'testacc','#B26B4A',0,'2026-09-21 15:05:09.247454','2026-09-21 15:05:09.247454'),(3,3,'123','#613321',0,'2026-09-21 15:32:11.158163','2026-09-21 15:32:11.158163'),(4,5,'acctest','#C7804F',0,'2026-09-21 15:43:46.977505','2026-09-21 15:43:46.977505'),(5,6,'qqqq','#F5C2A3',0,'2026-09-21 15:51:52.891380','2026-09-21 15:51:52.891380'),(6,7,'test','#F5C2A3',0,'2026-09-22 01:12:01.278907','2026-09-22 01:12:01.278907'),(7,8,'코카콜라맛있다','#7A4229',0,'2026-09-22 05:35:00.801621','2026-09-22 05:35:00.801621'),(8,9,'서여니','#F5C2A3',0,'2026-09-22 05:59:57.709599','2026-09-22 05:59:57.709599'),(9,11,'쮸코코','#F5C2A3',0,'2026-09-22 06:16:02.005333','2026-09-22 06:16:02.005333'),(10,10,'박설희','#F5C2A3',0,'2026-09-22 06:16:46.097230','2026-09-22 06:16:46.097230'),(11,12,'없다캐라','#FFF2E0',0,'2026-09-22 06:19:53.949154','2026-09-22 06:19:53.949154'),(12,13,'whdydwn','#FFF2E0',0,'2026-09-23 04:55:00.842715','2026-09-23 04:55:00.842715'),(13,14,'서연','#F5C2A3',0,'2026-09-23 08:02:51.207291','2026-09-23 08:02:51.207291'),(14,4,'qq3','#F5C2A3',0,'2026-09-23 17:49:13.162608','2026-09-23 17:49:13.162608'),(15,15,'jj','#F5C2A3',0,'2026-09-24 12:08:00.014968','2026-09-24 12:08:00.014968'),(16,16,'jj1','#F5C2A3',0,'2026-09-24 12:13:52.574891','2026-09-24 12:13:52.574891'),(17,17,'jjj','#F5C2A3',0,'2026-09-24 12:19:55.693559','2026-09-24 12:19:55.693559'),(18,18,'백백','#613321',0,'2026-09-24 12:21:15.921615','2026-09-24 12:21:15.921615'),(19,19,'백백백','#B26B4A',0,'2026-09-24 12:26:45.493923','2026-09-24 12:26:45.493923'),(20,20,'사과','#F5C2A3',0,'2026-09-24 21:37:44.219978','2026-09-24 21:37:44.219978'),(21,21,'banana','#613321',0,'2026-09-24 21:39:48.325413','2026-09-24 21:39:48.325413'),(22,22,'apple','#F5C2A3',0,'2026-09-24 21:49:34.961602','2026-09-24 21:49:34.961602'),(23,23,'pq','#EBAD7A',0,'2026-09-25 07:17:21.792835','2026-09-25 07:17:21.792835'),(24,24,'jjjj','#F5C2A3',0,'2026-09-25 12:10:21.365896','2026-09-25 12:10:21.365896'),(25,25,'서연잉','#F5C2A3',0,'2026-09-25 12:12:09.986364','2026-09-25 12:12:09.986364'),(26,26,'zxcv','#FABA94',0,'2026-09-25 13:08:59.259025','2026-09-25 13:08:59.259025'),(27,28,'쫄따구353','#F5C2A3',0,'2026-09-25 19:51:17.148230','2026-09-25 19:51:17.148230'),(28,29,'쫄따구241','#F5C2A3',0,'2026-09-25 19:52:27.214645','2026-09-25 19:52:27.214645'),(29,30,'백백백백','#613321',0,'2026-09-26 11:53:21.304802','2026-09-26 11:53:21.304802'),(30,31,'jjjjj','#F5C2A3',0,'2026-09-26 12:03:37.025541','2026-09-26 12:03:37.025541'),(31,32,'서여닁','#F5C2A3',0,'2026-09-27 12:16:09.941265','2026-09-27 12:16:09.941265'),(32,34,'써여이','#F5C2A3',0,'2026-09-27 12:31:12.314677','2026-09-27 12:31:12.314677'),(33,35,'미미미미','#F5C2A3',0,'2026-09-27 12:57:03.946860','2026-09-27 12:57:03.946860'),(34,36,'바바보','#F5C2A3',0,'2026-09-27 13:11:23.967209','2026-09-27 13:11:23.967209'),(35,37,'111','#F5C2A3',0,'2026-09-27 14:00:37.211104','2026-09-27 14:00:37.211104'),(36,38,'1112','#F5C2A3',0,'2026-09-27 14:13:21.736996','2026-09-27 14:13:21.736996'),(37,39,'rkskek','#F5C2A3',0,'2026-09-27 14:45:58.906948','2026-09-27 14:45:58.906948'),(38,40,'qqqeeedsef','#F5C2A3',0,'2026-09-27 18:32:31.597737','2026-09-27 18:32:31.597737'),(39,41,'ttt','#F5C2A3',0,'2026-09-27 20:34:26.038388','2026-09-27 20:34:26.038388'),(40,42,'ffff','#F5C2A3',0,'2026-09-27 23:53:24.400750','2026-09-27 23:53:24.400750'),(41,43,'서여닝','#F5C2A3',0,'2026-09-28 00:15:39.352076','2026-09-28 00:15:39.352076'),(42,44,'test7','#F5C2A3',0,'2026-09-28 00:31:46.632212','2026-09-28 00:31:46.632212');
/*!40000 ALTER TABLE `characters` ENABLE KEYS */;
UNLOCK TABLES;
DROP TABLE IF EXISTS `player_inventories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `player_inventories` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned NOT NULL,
  `item_id` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `quantity` int unsigned NOT NULL DEFAULT '0',
  `updated_at` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_inventory_user_item` (`user_id`,`item_id`),
  CONSTRAINT `fk_inventory_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=65 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

LOCK TABLES `player_inventories` WRITE;
/*!40000 ALTER TABLE `player_inventories` DISABLE KEYS */;
INSERT INTO `player_inventories` VALUES (1,1,'sea_heart_fragment',5,'2026-09-27 13:25:06.906251'),(4,3,'sea_heart_fragment',0,'2026-09-25 12:56:37.847457'),(5,25,'sea_heart_fragment',0,'2026-09-25 12:56:03.510949'),(6,19,'sea_heart_fragment',0,'2026-09-27 15:16:32.095578'),(7,24,'sea_heart_fragment',2,'2026-09-25 12:50:35.675684'),(14,28,'sea_heart_fragment',2,'2026-09-28 00:39:14.221095'),(17,31,'sea_heart_fragment',0,'2026-09-26 12:25:19.083615'),(35,35,'sea_heart_fragment',1,'2026-09-27 13:08:46.354196'),(37,34,'sea_heart_fragment',3,'2026-09-27 13:25:06.919846'),(49,36,'sea_heart_fragment',1,'2026-09-27 13:25:06.894359'),(56,39,'sea_heart_fragment',0,'2026-09-27 15:15:51.582650'),(59,43,'sea_heart_fragment',1,'2026-09-28 00:34:47.320725'),(60,8,'sea_heart_fragment',1,'2026-09-28 00:34:47.381412'),(62,44,'sea_heart_fragment',0,'2026-09-28 00:39:40.839878');
/*!40000 ALTER TABLE `player_inventories` ENABLE KEYS */;
UNLOCK TABLES;
DROP TABLE IF EXISTS `reward_claims`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `reward_claims` (
  `id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned NOT NULL,
  `game_id` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `match_key` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `claimed_at` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_claims_user_match` (`user_id`,`match_key`),
  KEY `idx_claims_user_time` (`user_id`,`claimed_at`),
  CONSTRAINT `fk_claims_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=65 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

LOCK TABLES `reward_claims` WRITE;
/*!40000 ALTER TABLE `reward_claims` DISABLE KEYS */;
INSERT INTO `reward_claims` VALUES (1,1,'sword','sword:90a7b8e5054340b089da909aec17def7','2026-09-25 08:56:48.973377'),(2,1,'sword','sword:21d8eb6df654470bb2d4ea93b30f47f1','2026-09-25 08:58:21.989328'),(3,1,'sword','sword:b27f1a1fee444899b1d357674bd1fbdc','2026-09-25 09:38:43.377325'),(4,3,'sword','sword:66028be89682462db2d9bd368157d465','2026-09-25 12:38:00.761244'),(5,25,'sword','sword:07b3d059b2074703a71b42a348ede07c','2026-09-25 12:38:00.803231'),(6,19,'sword','sword:becd4025c24b4cde95118f509546e3a1','2026-09-25 12:38:02.140070'),(7,24,'sword','sword:54fa6a032c8b4eb2a11449fe6a2be9bf','2026-09-25 12:38:02.169291'),(8,19,'mining','mining:d6527f96aee94f12af3a24a300cb566b','2026-09-25 12:50:26.838757'),(9,1,'mining','mining:f1e5e2303c7a442cbc82981f85d829d7','2026-09-25 12:50:26.853318'),(10,24,'mining','mining:65ffcca622884e74ac454fc352b8cb54','2026-09-25 12:50:35.675684'),(11,3,'mining','mining:ca51d2ec9a5445f2a01af94e8b869739','2026-09-25 12:50:35.687499'),(12,25,'mining','mining:ac3c550da6df436787a6aaa7b0a4b70b','2026-09-25 12:50:35.709089'),(13,19,'sword','sword:1162797000974247839f322778c00de5','2026-09-26 12:10:57.132629'),(14,28,'sword','sword:9b3d108566d84f128c0fba99a86621a8','2026-09-26 12:12:40.687426'),(15,19,'sword','sword:df253634da074a60be148919887b3898','2026-09-26 12:12:40.880764'),(16,19,'mining','mining:6a0dd8b6464343c0b0c0da9c69c354ea','2026-09-26 12:15:47.556826'),(17,31,'mining','mining:a41e2d130cad46219b3b11c39fab0448','2026-09-26 12:15:47.568309'),(18,28,'mining','mining:015025edcb384865a63b7835e1444682','2026-09-26 12:15:47.582947'),(19,1,'mining','mining:01fae14ca7984f58b049a83106dcfa17','2026-09-26 12:15:47.594421'),(20,1,'ship','ship:28cc1ef48a3e4d6b8dcc3b1fbd6f0fb8','2026-09-26 12:21:38.047192'),(21,19,'ship','ship:6fb6226cb00f4143b952054075317245','2026-09-26 12:21:38.061025'),(22,31,'ship','ship:a712d4ac3d824a8cb2a20f1c256ecda9','2026-09-26 12:21:38.070363'),(23,28,'ship','ship:3ea8b4bb5ea741668c031e6fbaef8fe3','2026-09-26 12:21:38.157714'),(24,1,'sword','sword:092cca5a3e30426ea5e1dcd1651e9ffd','2026-09-27 05:18:01.434299'),(25,1,'sword','sword:d15f29d6b15c461cbf3a18d83a57af1f','2026-09-27 05:18:43.591441'),(26,1,'sword','sword:66066b0f05ce4831bff274c71c1d0014','2026-09-27 05:31:10.669077'),(27,1,'sword','sword:06fd39fd0e0445f3acb0c6e68cf02734','2026-09-27 05:32:03.286667'),(28,1,'sword','sword:133f2100967a45f0902b6658143e8daa','2026-09-27 05:32:32.838158'),(29,1,'sword','sword:c5736c680d50449f8dbf7e14b2504467','2026-09-27 05:43:59.948434'),(30,1,'sword','sword:93cdeefc06a44172896580b706813362','2026-09-27 05:44:36.828239'),(31,1,'sword','sword:18f139e2559e48aeb94b9d97cd206741','2026-09-27 10:50:03.981748'),(32,1,'sword','sword:acba2d712929416ca943eacc6598ed1a','2026-09-27 10:50:49.791466'),(33,1,'sword','sword:233bc0c7c5194ed6998ad8f7534af3fe','2026-09-27 12:46:21.879209'),(34,19,'ship','ship:cc01cc1b595c42939a267f97f48a1489','2026-09-27 12:59:30.500714'),(35,35,'ship','ship:ec8dab3dc60545d49e014b30e153fc1f','2026-09-27 12:59:30.664715'),(36,1,'ship','ship:dc78d6925ae141b59b32b2bcc5cb871f','2026-09-27 12:59:30.805270'),(37,34,'ship','ship:773ec1ed24f047149add4aee79c23104','2026-09-27 12:59:30.827209'),(38,19,'mining','mining:f4f0b31032844ddd9f065cbf945e9d8e','2026-09-27 13:02:19.847663'),(39,1,'mining','mining:0adb034ccb444683a85f8ac2dff23bcc','2026-09-27 13:02:19.862151'),(40,35,'mining','mining:1e56d69c97e14d7a90941c8cefef3c4c','2026-09-27 13:02:19.882727'),(41,34,'mining','mining:068e2a3b0f1e4b8085f84ac377075317','2026-09-27 13:02:19.894624'),(42,19,'sword','sword:44d97c0bfec045a8878b1e868311d692','2026-09-27 13:04:00.175541'),(43,35,'sword','sword:f1f3c5d83bfd4683a61fc064a4c00c54','2026-09-27 13:04:00.212617'),(44,19,'mining','mining:62e4541a5e6b40ff85a0b68c1075e7aa','2026-09-27 13:08:46.324743'),(45,35,'mining','mining:6fcbd10a4ed44c518414658adfffdf6f','2026-09-27 13:08:46.354196'),(46,19,'mining','mining:16bb1a99bde546da97043bff95516523','2026-09-27 13:15:34.900808'),(47,1,'mining','mining:0c76268cdd8b409b9b576b680c3f58b4','2026-09-27 13:15:34.917892'),(48,34,'mining','mining:c7be6c644e294840b321b1c82292a2bf','2026-09-27 13:15:34.932259'),(49,36,'mining','mining:99f5af83614b464ba563eb38ae2bd8bb','2026-09-27 13:15:34.945465'),(50,1,'sword','sword:9b27f80eb7ab45ab963cc271accc298b','2026-09-27 13:17:12.888833'),(51,36,'sword','sword:42db672c31e941f3a2b6ceca9e954a82','2026-09-27 13:17:12.927387'),(52,19,'ship','ship:d8628c5d6a0d43ea849ad9a185b0b407','2026-09-27 13:25:06.872586'),(53,36,'ship','ship:d47fc771ed614f429af0cd590f9b3cb0','2026-09-27 13:25:06.894359'),(54,1,'ship','ship:bded6e91ef9843e29700a25f65f29737','2026-09-27 13:25:06.906251'),(55,34,'ship','ship:4f8ad23479524a79b04fbcb0311d49e6','2026-09-27 13:25:06.919846'),(56,39,'sword','sword:9b6d55b660c443a495524ab32a450643','2026-09-27 14:52:49.353347'),(57,39,'sword','sword:21e57d1a51724979a2c43c6655bf7394','2026-09-27 15:00:06.688713'),(58,39,'sword','sword:dc4ba6753261493d9699ee1c61a5854c','2026-09-27 15:01:17.216231'),(59,43,'ship','ship:efb4ae3faf1c4eadb02b9185de740a3e','2026-09-28 00:34:47.320725'),(60,8,'ship','ship:cb0e6083f09943999423d33ce9be53d7','2026-09-28 00:34:47.381412'),(61,28,'ship','ship:85cb983cf821473ea1170019cd545575','2026-09-28 00:34:47.394789'),(62,44,'ship','ship:2263869f45ee4554a58fbee1bfca8b7a','2026-09-28 00:34:47.409770'),(63,44,'sword','sword:dee09758bca049cb9d25a19b73dccd62','2026-09-28 00:39:13.994949'),(64,28,'sword','sword:f809fa2e4dc043adbd3430c66b47d3e7','2026-09-28 00:39:14.221095');
/*!40000 ALTER TABLE `reward_claims` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;


SET FOREIGN_KEY_CHECKS=1;
