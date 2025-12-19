SET search_path TO voting, public;

-- 1.SELECT
-- все пользователи
SELECT id, email, full_name FROM users;

-- все активные сессии
SELECT title, start_at, end_at
FROM voting_sessions
WHERE is_published = TRUE;

-- все кандидаты конкретной сессии
SELECT full_name, description
FROM candidates
WHERE session_id = (
  SELECT id FROM voting_sessions WHERE title = 'Выборы в студсовет'
);

-- 2.UPDATE
-- имя пользователя
UPDATE users
SET full_name = 'Alice Updated', updated_at = now()
WHERE email = 'user1@example.com';

-- закрыть
UPDATE voting_sessions
SET is_published = FALSE
WHERE title = 'Выборы в студсовет';

-- 3. DELETE
DELETE FROM notifications
WHERE type = 'INFO';

-- невалидные голоса
DELETE FROM votes
WHERE is_valid = FALSE;

-- 4. Подзапросы
-- пользователи, которые голосовали
SELECT full_name
FROM users
WHERE id IN (SELECT user_id FROM votes WHERE user_id IS NOT NULL);



select *
from votes
where user_id in (select id from users where full_name = '');


-- количество голосов у каждой сессии
SELECT
  title,
  (SELECT COUNT(*) FROM votes v
   WHERE v.candidate_id IN (
     SELECT c.id FROM candidates c WHERE c.session_id = vs.id
   )) AS total_votes
FROM voting_sessions vs;



-- есть ли хоть один голос у пользователя
SELECT EXISTS (
  SELECT 1 FROM votes WHERE user_id = (
    SELECT id FROM users WHERE email = 'user1@example.com'
  )
) AS user1_voted;

-- 5. regex
-- пользователи с example-доменом
SELECT email FROM users
WHERE email ~* '@example\.com$';

-- имена, начинающиеся с "А"
SELECT full_name FROM users
WHERE full_name ~ '^А';

-- исключить имена с цифрами
SELECT full_name FROM users
WHERE full_name !~ '\d';

-- сессии, в названии которых есть "опрос"
SELECT title FROM voting_sessions
WHERE title ~* 'опрос';

SELECT id, title, NOT is_published AS hidden
FROM voting_sessions;

-- 6. Фильтрация, сортировка, агрегаты
-- LIKE но без учета регистра в pg
SELECT * FROM users WHERE full_name ILIKE '%alice%';

-- BETWEEN
SELECT * FROM voting_sessions
WHERE start_at BETWEEN now() - INTERVAL '10 days' AND now() + INTERVAL '10 days';

-- IS NULL
SELECT * FROM notifications WHERE body IS NULL;

SELECT
  user_id,
  COUNT(*) AS votes_count
FROM votes
WHERE user_id IS NOT NULL
GROUP BY user_id
HAVING COUNT(*) > 0;

-- ORDER BY, LIMIT
SELECT * FROM users ORDER BY created_at DESC LIMIT 3;

-- Агрегация
SELECT COUNT(*) AS total_users, MAX(created_at) AS newest_user FROM users;