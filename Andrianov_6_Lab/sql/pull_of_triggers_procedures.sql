SET search_path TO voting, public;

-- users.updated_at авто-обновление
CREATE OR REPLACE FUNCTION voting.fn_users_set_updated_at()
RETURNS TRIGGER AS
$$
BEGIN
NEW.updated_at=now();
RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_users_set_updated_at ON voting.users;
CREATE TRIGGER trg_users_set_updated_at
BEFORE UPDATE
ON voting.users
FOR EACH ROW
EXECUTE FUNCTION voting.fn_users_set_updated_at();

-- votes: запрет голосовать вне окна сессии + запрет за не опубликованную
CREATE OR REPLACE FUNCTION voting.fn_votes_check_session_time()
RETURNS TRIGGER AS
$$
DECLARE
v_session_id uuid;
v_start timestamptz;
v_end timestamptz;
v_pub boolean;
BEGIN
SELECT c.session_id INTO v_session_id
FROM voting.candidates c
WHERE c.id=NEW.candidate_id;

SELECT vs.start_at, vs.end_at, vs.is_published
INTO v_start, v_end, v_pub
FROM voting.voting_sessions vs
WHERE vs.id=v_session_id;

IF v_pub IS DISTINCT FROM TRUE THEN
RAISE EXCEPTION 'Session is not published';
END IF;

IF now()<v_start OR now()>v_end THEN
RAISE EXCEPTION 'Voting is closed';
END IF;

RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_votes_check_session_time ON voting.votes;
CREATE TRIGGER trg_votes_check_session_time
BEFORE INSERT
ON voting.votes
FOR EACH ROW
EXECUTE FUNCTION voting.fn_votes_check_session_time();

CALL

-- votes: уведомление пользователю о принятом голосе (если user_id есть)
CREATE OR REPLACE FUNCTION voting.fn_notify_on_vote()
RETURNS TRIGGER AS
$$
DECLARE
v_title text;
v_anonymous boolean;
BEGIN
SELECT s.anonymous, vs.title
INTO v_anonymous, v_title
FROM voting.candidates c
JOIN voting.voting_sessions vs ON vs.id=c.session_id
JOIN voting.voting_settings s ON s.session_id=vs.id
WHERE c.id=NEW.candidate_id;

IF v_anonymous=FALSE AND NEW.user_id IS NOT NULL THEN
INSERT INTO voting.notifications(id,user_id,type,title,body,is_read,created_at)
VALUES(gen_random_uuid(),NEW.user_id,'VOTE_CONFIRMED','Ваш голос учтён',v_title,FALSE,now());
END IF;

RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_notify_on_vote ON voting.votes;
CREATE TRIGGER trg_notify_on_vote
AFTER INSERT
ON voting.votes
FOR EACH ROW
EXECUTE FUNCTION voting.fn_notify_on_vote();

-- votes: при анонимном голосовании обнулять user_id
CREATE OR REPLACE FUNCTION voting.fn_votes_apply_anonymous()
RETURNS TRIGGER AS
$$
DECLARE
v_anonymous boolean;
BEGIN
SELECT s.anonymous INTO v_anonymous
FROM voting.candidates c
JOIN voting.voting_settings s ON s.session_id=c.session_id
WHERE c.id=NEW.candidate_id;

IF v_anonymous=TRUE THEN
NEW.user_id=NULL;
END IF;

RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_votes_apply_anonymous ON voting.votes;
CREATE TRIGGER trg_votes_apply_anonymous
BEFORE INSERT
ON voting.votes
FOR EACH ROW
EXECUTE FUNCTION voting.fn_votes_apply_anonymous();


-- votes: пересчет results.total_votes для сессии
CREATE OR REPLACE FUNCTION voting.fn_votes_recalc_total()
RETURNS TRIGGER AS
$$
DECLARE
v_session_id uuid;
v_total int;
BEGIN
IF TG_OP='DELETE' THEN
SELECT c.session_id INTO v_session_id FROM voting.candidates c WHERE c.id=OLD.candidate_id;
ELSE
SELECT c.session_id INTO v_session_id FROM voting.candidates c WHERE c.id=NEW.candidate_id;
END IF;

SELECT COUNT(v.id) INTO v_total
FROM voting.votes v
JOIN voting.candidates c ON c.id=v.candidate_id
WHERE c.session_id=v_session_id AND v.is_valid=TRUE;

INSERT INTO voting.results(session_id,generated_at,total_votes,payload,signature)
VALUES(v_session_id,now(),COALESCE(v_total,0),'[]'::jsonb,NULL)
ON CONFLICT(session_id) DO UPDATE
SET total_votes=EXCLUDED.total_votes,
    generated_at=EXCLUDED.generated_at;

IF TG_OP='DELETE' THEN
RETURN OLD;
END IF;
RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_votes_recalc_total_ins ON voting.votes;
DROP TRIGGER IF EXISTS trg_votes_recalc_total_upd ON voting.votes;
DROP TRIGGER IF EXISTS trg_votes_recalc_total_del ON voting.votes;

SELECT id, email, full_name
FROM voting.users
ORDER BY created_at DESC;

SELECT id, candidate_id, user_id, cast_at
FROM voting.votes
ORDER BY cast_at DESC
LIMIT 5;

SELECT id, user_id, type, title, body, created_at
FROM voting.notifications
WHERE user_id = '0eb536f8-0bb1-4b07-a883-93b6dfefc5cd'
ORDER BY created_at DESC;

SELECT
  c.id AS candidate_id,
  c.session_id,
  s.session_id AS settings_session_id,
  s.anonymous,
  vs.title,
  vs.is_published
FROM voting.candidates c
JOIN voting.voting_sessions vs ON vs.id = c.session_id
LEFT JOIN voting.voting_settings s ON s.session_id = vs.id
WHERE c.id = 'af533306-6f6e-4a95-9e0b-ac31a01a2c1a';

SELECT tgname, tgenabled
FROM pg_trigger
WHERE tgrelid = 'voting.votes'::regclass
  AND NOT tgisinternal;



CALL voting.sp_cast_vote(
  '0eb536f8-0bb1-4b07-a883-93b6dfefc5cd',
  'af533' ||
  '' ||
  '' ||
  '' ||
  '06-6f6e-4a95-9e0b-ac31a01a2c1a',
  1
);


CREATE TRIGGER trg_votes_recalc_total_ins
AFTER INSERT
ON voting.votes
FOR EACH ROW
EXECUTE FUNCTION voting.fn_votes_recalc_total();

CREATE TRIGGER trg_votes_recalc_total_upd
AFTER UPDATE OF is_valid,candidate_id
ON voting.votes
FOR EACH ROW
EXECUTE FUNCTION voting.fn_votes_recalc_total();

CREATE TRIGGER trg_votes_recalc_total_del
AFTER DELETE
ON voting.votes
FOR EACH ROW
EXECUTE FUNCTION voting.fn_votes_recalc_total();


-- voting_sessions: при публикации разослать уведомления активным
CREATE OR REPLACE FUNCTION voting.fn_notify_on_publish()
RETURNS TRIGGER AS
$$
BEGIN
IF OLD.is_published=FALSE AND NEW.is_published=TRUE THEN
INSERT INTO voting.notifications(id,user_id,type,title,body,is_read,created_at)
SELECT gen_random_uuid(),
       u.id,
       'SESSION_PUBLISHED',
       'Опубликована сессия',
       NEW.title,
       FALSE,
       now()
FROM voting.users u
JOIN voting.user_statuses us ON us.id=u.status_id
WHERE us.code='ACTIVE';
END IF;
RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_notify_on_publish ON voting.voting_sessions;
CREATE TRIGGER trg_notify_on_publish
AFTER UPDATE OF is_published
ON voting.voting_sessions
FOR EACH ROW
EXECUTE FUNCTION voting.fn_notify_on_publish();


-- PROCEDURES

-- Регистрация пользователя
CREATE OR REPLACE PROCEDURE voting.sp_register_user(
p_email text,
p_password_hash text,
p_full_name text,
p_role_code text DEFAULT 'USER',
p_status_code text DEFAULT 'ACTIVE'
)
LANGUAGE plpgsql
AS
$$
DECLARE
v_role_id uuid;
v_status_id uuid;
BEGIN
SELECT id INTO v_role_id FROM voting.roles WHERE code=p_role_code;
IF v_role_id IS NULL THEN
RAISE EXCEPTION 'Role % not found', p_role_code;
END IF;

SELECT id INTO v_status_id FROM voting.user_statuses WHERE code=p_status_code;
IF v_status_id IS NULL THEN
RAISE EXCEPTION 'Status % not found', p_status_code;
END IF;

INSERT INTO voting.users(email,password_hash,full_name,role_id,status_id)
VALUES(p_email,p_password_hash,p_full_name,v_role_id,v_status_id);
END;
$$;

-- Создать сессию (без настроек)
CREATE OR REPLACE PROCEDURE voting.sp_create_session(
p_title text,
p_description text,
p_created_by uuid,
p_start_at timestamptz,
p_end_at timestamptz,
p_visibility text DEFAULT 'private'
)
LANGUAGE plpgsql
AS
$$
BEGIN
INSERT INTO voting.voting_sessions(id,title,description,created_by,start_at,end_at,is_published,visibility,created_at)
VALUES(gen_random_uuid(),p_title,p_description,p_created_by,p_start_at,p_end_at,FALSE,p_visibility,now());
END;
$$;

-- Создать/обновить настройки сессии
CREATE OR REPLACE PROCEDURE voting.sp_upsert_settings(
p_session_id uuid,
p_anonymous boolean,
p_multi_select boolean,
p_max_choices int
)
LANGUAGE plpgsql
AS
$$
BEGIN
INSERT INTO voting.voting_settings(session_id,anonymous,multi_select,max_choices)
VALUES(p_session_id,p_anonymous,p_multi_select,p_max_choices)
ON CONFLICT(session_id) DO UPDATE
SET anonymous=EXCLUDED.anonymous,
    multi_select=EXCLUDED.multi_select,
    max_choices=EXCLUDED.max_choices;
END;
$$;

-- Добавить кандидата
CREATE OR REPLACE PROCEDURE voting.sp_add_candidate(
p_session_id uuid,
p_candidate_type_code text,
p_full_name text,
p_description text DEFAULT NULL
)
LANGUAGE plpgsql
AS
$$
DECLARE
v_type_id uuid;
BEGIN
SELECT id INTO v_type_id FROM voting.candidate_types WHERE code=p_candidate_type_code;
IF v_type_id IS NULL THEN
RAISE EXCEPTION 'Candidate type % not found', p_candidate_type_code;
END IF;

INSERT INTO voting.candidates(id,session_id,candidate_type_id,full_name,description)
VALUES(gen_random_uuid(),p_session_id,v_type_id,p_full_name,p_description);
END;
$$;

-- Проголосовать (включает триггеры: время/анонимность/уведомление/пересчет)
CREATE OR REPLACE PROCEDURE voting.sp_cast_vote(
p_user_id uuid,
p_candidate_id uuid,
p_weight numeric DEFAULT 1
)
LANGUAGE plpgsql
AS
$$
BEGIN
INSERT INTO voting.votes(id,candidate_id,user_id,cast_at,weight,is_valid)
VALUES(gen_random_uuid(),p_candidate_id,p_user_id,now(),p_weight,TRUE);
END;
$$;

-- Опубликовать сессию (включает триггер рассылки уведомлений)
CREATE OR REPLACE PROCEDURE voting.sp_publish_session(
p_session_id uuid
)
LANGUAGE plpgsql
AS
$$
BEGIN
UPDATE voting.voting_sessions
SET is_published=TRUE
WHERE id=p_session_id;
END;
$$;

-- Пометить уведомление прочитанным
CREATE OR REPLACE PROCEDURE voting.sp_mark_notification_read(
p_notification_id uuid,
p_user_id uuid
)
LANGUAGE plpgsql
AS
$$
BEGIN
UPDATE voting.notifications
SET is_read=TRUE
WHERE id=p_notification_id AND user_id=p_user_id;
END;
$$;

-- Записать лог действия
CREATE OR REPLACE PROCEDURE voting.sp_log_action(
p_user_id uuid,
p_action text,
p_entity_type text DEFAULT NULL,
p_entity_id text DEFAULT NULL,
p_meta jsonb DEFAULT NULL
)
LANGUAGE plpgsql
AS
$$
BEGIN
INSERT INTO voting.user_logs(id,user_id,action,entity_type,entity_id,meta,created_at)
VALUES(gen_random_uuid(),p_user_id,p_action,p_entity_type,p_entity_id,p_meta,now());
END;
$$;
