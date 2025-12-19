CREATE SCHEMA IF NOT EXISTS voting;
SET search_path TO voting, public;


CREATE TABLE IF NOT EXISTS roles (
  id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS user_statuses (
  id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS candidate_types (
  id   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS users (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email TEXT NOT NULL UNIQUE,
  password_hash TEXT NOT NULL,
  full_name TEXT NOT NULL,
  role_id UUID NOT NULL REFERENCES roles(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
  status_id UUID NOT NULL REFERENCES user_statuses(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CONSTRAINT email_format_chk CHECK (position('@' in email) > 1)
);

CREATE TABLE IF NOT EXISTS voting_sessions (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  title TEXT NOT NULL,
  description TEXT,
  created_by UUID NOT NULL REFERENCES users(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
  start_at TIMESTAMPTZ NOT NULL,
  end_at TIMESTAMPTZ NOT NULL,
  is_published BOOLEAN NOT NULL DEFAULT FALSE,
  visibility TEXT NOT NULL DEFAULT 'private',
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  CONSTRAINT visibility_chk CHECK (visibility IN ('public','private')),
  CONSTRAINT dates_order_chk CHECK (end_at > start_at)
);

CREATE TABLE IF NOT EXISTS voting_settings (
  session_id UUID PRIMARY KEY REFERENCES voting_sessions(id) ON UPDATE CASCADE ON DELETE CASCADE,
  anonymous BOOLEAN NOT NULL DEFAULT TRUE,
  multi_select BOOLEAN NOT NULL DEFAULT FALSE,
  max_choices INT NOT NULL DEFAULT 1,
  require_confirmed_email BOOLEAN NOT NULL DEFAULT FALSE,
  allow_vote_change_until_close BOOLEAN NOT NULL DEFAULT FALSE,
  CONSTRAINT max_choices_positive_chk CHECK (max_choices >= 1),
  CONSTRAINT multi_select_logic_chk   CHECK ((multi_select = FALSE AND max_choices = 1) OR (multi_select = TRUE))
);

CREATE TABLE IF NOT EXISTS candidates (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  session_id UUID NOT NULL REFERENCES voting_sessions(id) ON UPDATE CASCADE ON DELETE CASCADE,
  candidate_type_id UUID NOT NULL REFERENCES candidate_types(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
  full_name TEXT NOT NULL,
  description TEXT
);

CREATE TABLE IF NOT EXISTS votes (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  candidate_id UUID NOT NULL REFERENCES candidates(id) ON UPDATE CASCADE ON DELETE CASCADE,
  user_id UUID REFERENCES users(id) ON UPDATE RESTRICT ON DELETE SET NULL,
  cast_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  weight NUMERIC(10,2) NOT NULL DEFAULT 1,
  is_valid BOOLEAN NOT NULL DEFAULT TRUE,
  CONSTRAINT weight_positive_chk CHECK (weight > 0)
);

CREATE TABLE IF NOT EXISTS results (
  session_id UUID PRIMARY KEY REFERENCES voting_sessions(id) ON UPDATE CASCADE ON DELETE CASCADE,
  generated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  total_votes INT NOT NULL DEFAULT 0 CHECK (total_votes >= 0),
  payload JSONB NOT NULL DEFAULT '[]'::jsonb,
  signature TEXT
);

CREATE TABLE IF NOT EXISTS notifications (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID NOT NULL REFERENCES users(id) ON UPDATE RESTRICT ON DELETE CASCADE,
  type TEXT NOT NULL,
  title TEXT NOT NULL,
  body TEXT,
  is_read BOOLEAN NOT NULL DEFAULT FALSE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS user_logs (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID REFERENCES users(id) ON UPDATE RESTRICT ON DELETE SET NULL,
  action TEXT NOT NULL,
  entity_type TEXT,
  entity_id TEXT,
  meta JSONB,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- =========================
-- ИНДЕКСЫ
-- =========================

-- users
CREATE INDEX IF NOT EXISTS ix_users_role_id ON users(role_id);
CREATE INDEX IF NOT EXISTS ix_users_status_id ON users(status_id);

-- voting_sessions
CREATE INDEX IF NOT EXISTS ix_sessions_created_by ON voting_sessions(created_by);
CREATE INDEX IF NOT EXISTS ix_sessions_dates ON voting_sessions(start_at, end_at);
CREATE INDEX IF NOT EXISTS ix_sessions_published ON voting_sessions(is_published);
CREATE INDEX IF NOT EXISTS ix_sessions_visibility ON voting_sessions(visibility);

-- voting_settings
CREATE INDEX IF NOT EXISTS ix_settings_flags ON voting_settings(anonymous, multi_select);

-- candidates
CREATE INDEX IF NOT EXISTS ix_candidates_session_id ON candidates(session_id);
CREATE INDEX IF NOT EXISTS ix_candidates_type_id ON candidates(candidate_type_id);

-- votes
CREATE INDEX IF NOT EXISTS ix_votes_candidate_id ON votes(candidate_id);
CREATE INDEX IF NOT EXISTS ix_votes_user_id ON votes(user_id);
CREATE INDEX IF NOT EXISTS ix_votes_cast_at ON votes(cast_at);
CREATE INDEX IF NOT EXISTS ix_votes_valid ON votes(is_valid);

-- notifications
CREATE INDEX IF NOT EXISTS ix_notifications_user_id ON notifications(user_id);
CREATE INDEX IF NOT EXISTS ix_notifications_is_read ON notifications(is_read);

-- user_logs
CREATE INDEX IF NOT EXISTS ix_user_logs_user_id ON user_logs(user_id);
CREATE INDEX IF NOT EXISTS ix_user_logs_action ON user_logs(action);
CREATE INDEX IF NOT EXISTS ix_user_logs_created ON user_logs(created_at);
