-- Consulta somente leitura. Bind :session_id = UUID canônico (36 caracteres); não inclui credenciais.
SELECT ID, SESSION_ID, OUTCOME, RATING, NEEDED_HUMAN_HELP, SOURCE, CREATED_AT_UTC
FROM SESSION_EVALUATIONS
WHERE SESSION_ID = :session_id;

-- Deve retornar zero linhas: unicidade também garantida por UX_SESSION_EVAL_SESSION_ID.
SELECT SESSION_ID, COUNT(*) AS EVALUATION_COUNT
FROM SESSION_EVALUATIONS
GROUP BY SESSION_ID
HAVING COUNT(*) > 1;
