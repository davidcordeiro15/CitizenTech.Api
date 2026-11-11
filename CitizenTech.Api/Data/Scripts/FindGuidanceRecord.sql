-- Bind :request_id to the canonical lower-case UUID string (36 characters).
-- Metadata only: a success record does not prove that the citizen completed a task.
SELECT REQUEST_ID, SESSION_ID, CREATED_AT_UTC, ACTION, RESULT, ERROR_CODE
FROM GUIDANCE_RECORDS
WHERE REQUEST_ID = :request_id;
