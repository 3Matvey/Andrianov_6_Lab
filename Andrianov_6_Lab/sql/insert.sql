SET search_path TO voting, public;

INSERT INTO roles (code, name) VALUES
  ('ADMIN', 'Administrator'),
  ('USER',  'User');

INSERT INTO user_statuses (code, name) VALUES
  ('ACTIVE',  'Active'),
  ('BLOCKED', 'Blocked');

INSERT INTO candidate_types (code, name) VALUES
  ('PERSON', 'Person'),
  ('OPTION', 'Option');

INSERT INTO users (email, password_hash, full_name, role_id, status_id)
SELECT 'admin@example.com', 'hash_admin', 'Admin User', r.id, s.id
FROM roles r, user_statuses s
WHERE r.code = 'ADMIN' AND s.code = 'ACTIVE';

INSERT INTO users (email, password_hash, full_name, role_id, status_id)
SELECT 'user1@example.com', 'hash_user1', 'Alice Smith', r.id, s.id
FROM roles r, user_statuses s
WHERE r.code = 'USER' AND s.code = 'ACTIVE';

INSERT INTO users (email, password_hash, full_name, role_id, status_id)
SELECT 'user2@example.com', 'hash_user2', 'Bob Johnson', r.id, s.id
FROM roles r, user_statuses s
WHERE r.code = 'USER' AND s.code = 'ACTIVE';


-- Session 1: Single choice, not anonymous
INSERT INTO voting_sessions (title, description, created_by, start_at, end_at, is_published, visibility)
SELECT
  'Выборы в студсовет',
  'Выборы председателя студсовета',
  u.id,
  now() - INTERVAL '1 day',
  now() + INTERVAL '6 days',
  TRUE,
  'public'
FROM users u WHERE u.email = 'admin@example.com';


INSERT INTO voting_settings (session_id, anonymous, multi_select, max_choices, require_confirmed_email, allow_vote_change_until_close)
SELECT vs.id, FALSE, FALSE, 1, TRUE, FALSE
FROM voting_sessions vs
WHERE vs.title = 'Выборы в студсовет';


-- Session 2: Multi-choice, anonymous
INSERT INTO voting_sessions (title, description, created_by, start_at, end_at, is_published, visibility)
SELECT
  'Опрос по благоустройству',
  'Выберите до трёх приоритетов',
  u.id,
  now() - INTERVAL '2 hours',
  now() + INTERVAL '5 days',
  TRUE,
  'public'
FROM users u WHERE u.email = 'admin@example.com';


INSERT INTO voting_settings (session_id, anonymous, multi_select, max_choices, require_confirmed_email, allow_vote_change_until_close)
SELECT vs.id, TRUE, TRUE, 3, FALSE, TRUE
FROM voting_sessions vs
WHERE vs.title = 'Опрос по благоустройству';


-- -------------------------
-- Candidates
-- -------------------------

-- Session 1 (PERSON)
INSERT INTO candidates (session_id, candidate_type_id, full_name, description)
SELECT vs.id, ct.id, 'Иван Петров', 'Программа A'
FROM voting_sessions vs
JOIN candidate_types ct ON ct.code = 'PERSON'
WHERE vs.title = 'Выборы в студсовет';


INSERT INTO candidates (session_id, candidate_type_id, full_name, description)
SELECT vs.id, ct.id, 'Мария Иванова', 'Программа B'
FROM voting_sessions vs
JOIN candidate_types ct ON ct.code = 'PERSON'
WHERE vs.title = 'Выборы в студсовет';


-- Session 2 (OPTION)
INSERT INTO candidates (session_id, candidate_type_id, full_name, description)
SELECT vs.id, ct.id, 'Освещение двора', 'Лампы, фонари, тропинки'
FROM voting_sessions vs
JOIN candidate_types ct ON ct.code = 'OPTION'
WHERE vs.title = 'Опрос по благоустройству';


INSERT INTO candidates (session_id, candidate_type_id, full_name, description)
SELECT vs.id, ct.id, 'Детская площадка', 'Качели, горки, покрытие'
FROM voting_sessions vs
JOIN candidate_types ct ON ct.code = 'OPTION'
WHERE vs.title = 'Опрос по благоустройству';


INSERT INTO candidates (session_id, candidate_type_id, full_name, description)
SELECT vs.id, ct.id, 'Видеонаблюдение', 'Камеры во дворе'
FROM voting_sessions vs
JOIN candidate_types ct ON ct.code = 'OPTION'
WHERE vs.title = 'Опрос по благоустройству';


-- -------------------------
-- Votes
-- -------------------------

-- Non-anonymous vote for "Иван Петров" by user1
INSERT INTO votes (candidate_id, user_id, weight, is_valid)
SELECT c.id, u.id, 1, TRUE
FROM candidates c
JOIN voting_sessions vs ON vs.id = c.session_id
JOIN users u ON u.email = 'user1@example.com'
WHERE vs.title = 'Выборы в студсовет' AND c.full_name = 'Иван Петров';


-- Anonymous votes for Session 2
INSERT INTO votes (candidate_id, weight, is_valid)
SELECT c.id, 1, TRUE
FROM candidates c
JOIN voting_sessions vs ON vs.id = c.session_id
WHERE vs.title = 'Опрос по благоустройству' AND c.full_name IN ('Освещение двора', 'Детская площадка');


-- -------------------------
-- Notifications
-- -------------------------
INSERT INTO notifications (user_id, type, title, body)
SELECT u.id, 'SESSION_START', 'Стартовала сессия «Выборы в студсовет»', NULL
FROM users u WHERE u.email = 'user1@example.com';


INSERT INTO notifications (user_id, type, title, body)
SELECT u.id, 'VOTE_CONFIRMED', 'Ваш голос учтён', 'Спасибо за участие!'
FROM users u WHERE u.email = 'user1@example.com';


-- -------------------------
-- User Logs
-- -------------------------
INSERT INTO user_logs (user_id, action, meta)
SELECT u.id, 'LOGIN_SUCCESS', jsonb_build_object('ip','127.0.0.1','ua','seed')
FROM users u WHERE u.email = 'user1@example.com';


INSERT INTO user_logs (user_id, action, entity_type, entity_id, meta)
SELECT u.id, 'CREATE', 'SESSION', vs.id, jsonb_build_object('title', vs.title)
FROM users u
JOIN voting_sessions vs ON vs.title = 'Выборы в студсовет'
WHERE u.email = 'admin@example.com';


-- -------------------------
-- Results
-- -------------------------
INSERT INTO results (session_id, generated_at, total_votes, payload, signature)
SELECT
  vs.id,
  now(),
  COUNT(v.*),
    '[]'::jsonb,
  'seed_signature_1'
FROM voting_sessions vs
LEFT JOIN candidates c ON c.session_id = vs.id
LEFT JOIN votes v ON v.candidate_id = c.id
WHERE vs.title = 'Выборы в студсовет'
GROUP BY vs.id;