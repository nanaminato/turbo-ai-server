-- Registration email verification; additive migration for existing databases.
CREATE TABLE RegistrationEmailChallenges (
    EmailHash varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
    CodeHash varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    ExpiresAt datetime(6) NOT NULL,
    NextSendAt datetime(6) NOT NULL,
    AttemptsLeft int NOT NULL
);
