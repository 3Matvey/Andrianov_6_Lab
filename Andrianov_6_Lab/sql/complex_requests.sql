-- JOINs

SELECT u.id, u.email, r.name AS role_name
FROM voting.users u
JOIN voting.roles r ON u.role_id = r.id;

SELECT u.id, u.email, n.title AS notification_title
FROM voting.users u
LEFT JOIN voting.notifications n ON u.id = n.user_id;

SELECT n.id, n.title, u.email
FROM voting.notifications n
RIGHT JOIN voting.users u ON n.user_id = u.id;

SELECT u.email, n.title
FROM voting.users u
FULL JOIN voting.notifications n ON u.id = n.user_id;

SELECT ct.code AS candidate_type_code, r.name AS role_name
FROM voting.candidate_types ct
CROSS JOIN voting.roles r;

select vs.title, u.full_name, res.total_votes
from voting.voting_sessions vs
left join voting.results res on vs.id = res.session_id
left join voting.users u on u.id = vs.created_by;






-- self

SELECT l1.id, l1.user_id, l1.action AS action1, l2.action AS action2, l2.created_at AS action2_at
FROM voting.user_logs l1
JOIN voting.user_logs l2 ON l1.user_id = l2.user_id
WHERE l1.id <> l2.id;

-- aggregate

SELECT vs.title AS session_title, COUNT(v.id) AS votes_count
FROM voting.voting_sessions vs
JOIN voting.candidates c ON c.session_id = vs.id
JOIN voting.votes v ON v.candidate_id = c.id
GROUP BY vs.title;

SELECT u.email, COUNT(n.id) AS notifications_count
FROM voting.users u
LEFT JOIN voting.notifications n ON u.id = n.user_id
GROUP BY u.email;

-- window

SELECT v.candidate_id,
       v.weight,
       SUM(v.weight) OVER (PARTITION BY v.candidate_id) AS total_weight_per_candidate,
       ROUND(v.weight / SUM(v.weight) OVER (PARTITION BY v.candidate_id), 4) AS share
FROM voting.votes v;

select u.full_name from voting.users u
voting.voting_sessions vs on u.id = vs.created_by,
    sum() over (partition by u.full_name) as count
;


-- having

SELECT u.email
FROM voting.users u
JOIN voting.notifications n ON u.id = n.user_id
GROUP BY u.email, u.email
HAVING COUNT(n.id) > 1;

-- union

SELECT u.email AS identifier, 'user' AS entity_type
FROM voting.users u
UNION
SELECT vs.title AS identifier, 'session' AS entity_type
FROM voting.voting_sessions vs;

-- exists

SELECT u.id, u.email
FROM voting.users u
WHERE EXISTS (
    SELECT 1
    FROM voting.votes v
    WHERE v.user_id = u.id AND v.is_valid = TRUE
);

-- INSERT INTO SELECT

INSERT INTO voting.notifications (id, user_id, type, title, body, is_read, created_at)
SELECT gen_random_uuid(),
       u.id,
       'VOTE_THANKS',
       'Спасибо за участие в голосовании',
       'Ваш голос учтён',
       FALSE,
       now()
FROM voting.users u
WHERE EXISTS (
    SELECT 1
    FROM voting.votes v
    WHERE v.user_id = u.id AND v.is_valid = TRUE
);

-- CASE

SELECT v.id,
       v.weight,
       CASE
           WHEN v.weight < 1 THEN 'low'
           WHEN v.weight = 1 THEN 'normal'
           ELSE 'high'
       END AS weight_category
FROM voting.votes v;

-- EXPLAIN

EXPLAIN
SELECT vs.title, COUNT(v.id) AS votes_count
FROM voting.voting_sessions vs
JOIN voting.candidates c ON c.session_id = vs.id
JOIN voting.votes v ON v.candidate_id = c.id
GROUP BY vs.title;

-- view
