CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;
CREATE TABLE users (
    id uuid NOT NULL,
    email character varying(320) NOT NULL,
    display_name character varying(80) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE TABLE leaderboards (
    id uuid NOT NULL,
    name character varying(120) NOT NULL,
    description text,
    owner_id uuid NOT NULL,
    status character varying(20) NOT NULL,
    source character varying(20) NOT NULL,
    kind character varying(20) NOT NULL,
    visibility character varying(20) NOT NULL,
    starts_at timestamp with time zone,
    ends_at timestamp with time zone,
    plan character varying(20) NOT NULL,
    member_limit integer,
    plan_expires_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    published_at timestamp with time zone,
    CONSTRAINT pk_leaderboards PRIMARY KEY (id),
    CONSTRAINT fk_leaderboards_users_owner_id FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE events (
    id uuid NOT NULL,
    leaderboard_id uuid NOT NULL,
    name character varying(120) NOT NULL,
    description text,
    score_type character varying(20) NOT NULL,
    sort_direction character varying(20) NOT NULL,
    time_cap_seconds integer,
    position integer NOT NULL,
    scheduled_on date,
    CONSTRAINT pk_events PRIMARY KEY (id),
    CONSTRAINT fk_events_leaderboards_leaderboard_id FOREIGN KEY (leaderboard_id) REFERENCES leaderboards (id) ON DELETE CASCADE
);

CREATE TABLE memberships (
    leaderboard_id uuid NOT NULL,
    user_id uuid NOT NULL,
    role character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    invited_by_id uuid,
    invited_at timestamp with time zone NOT NULL,
    responded_at timestamp with time zone,
    CONSTRAINT pk_memberships PRIMARY KEY (leaderboard_id, user_id),
    CONSTRAINT fk_memberships_leaderboards_leaderboard_id FOREIGN KEY (leaderboard_id) REFERENCES leaderboards (id) ON DELETE CASCADE,
    CONSTRAINT fk_memberships_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE payments (
    id uuid NOT NULL,
    leaderboard_id uuid NOT NULL,
    user_id uuid NOT NULL,
    kind character varying(20) NOT NULL,
    amount_cents integer NOT NULL,
    currency character varying(3) NOT NULL,
    status character varying(20) NOT NULL,
    provider character varying(20),
    provider_ref character varying(255),
    created_at timestamp with time zone NOT NULL,
    paid_at timestamp with time zone,
    CONSTRAINT pk_payments PRIMARY KEY (id),
    CONSTRAINT fk_payments_leaderboards_leaderboard_id FOREIGN KEY (leaderboard_id) REFERENCES leaderboards (id) ON DELETE CASCADE,
    CONSTRAINT fk_payments_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE comments (
    id uuid NOT NULL,
    event_id uuid NOT NULL,
    user_id uuid NOT NULL,
    body character varying(1000) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    deleted_at timestamp with time zone,
    CONSTRAINT pk_comments PRIMARY KEY (id),
    CONSTRAINT fk_comments_events_event_id FOREIGN KEY (event_id) REFERENCES events (id) ON DELETE CASCADE,
    CONSTRAINT fk_comments_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE event_views (
    event_id uuid NOT NULL,
    user_id uuid NOT NULL,
    first_seen_at timestamp with time zone NOT NULL,
    last_seen_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_event_views PRIMARY KEY (event_id, user_id),
    CONSTRAINT fk_event_views_events_event_id FOREIGN KEY (event_id) REFERENCES events (id) ON DELETE CASCADE,
    CONSTRAINT fk_event_views_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE scores (
    id uuid NOT NULL,
    event_id uuid NOT NULL,
    user_id uuid NOT NULL,
    primary_value numeric(12,3) NOT NULL,
    secondary_value integer,
    is_capped boolean NOT NULL,
    is_rx boolean NOT NULL,
    notes character varying(500),
    submitted_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_scores PRIMARY KEY (id),
    CONSTRAINT fk_scores_events_event_id FOREIGN KEY (event_id) REFERENCES events (id) ON DELETE CASCADE,
    CONSTRAINT fk_scores_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE hearts (
    score_id uuid NOT NULL,
    user_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_hearts PRIMARY KEY (score_id, user_id),
    CONSTRAINT fk_hearts_scores_score_id FOREIGN KEY (score_id) REFERENCES scores (id) ON DELETE CASCADE,
    CONSTRAINT fk_hearts_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE INDEX ix_comments_event_id_created_at ON comments (event_id, created_at);

CREATE INDEX ix_comments_user_id ON comments (user_id);

CREATE INDEX ix_event_views_user_id ON event_views (user_id);

CREATE INDEX ix_events_leaderboard_id_position ON events (leaderboard_id, position);

CREATE INDEX ix_events_leaderboard_id_scheduled_on ON events (leaderboard_id, scheduled_on);

CREATE INDEX ix_hearts_user_id ON hearts (user_id);

CREATE INDEX ix_leaderboards_owner_id ON leaderboards (owner_id);

CREATE INDEX ix_leaderboards_visibility_status ON leaderboards (visibility, status);

CREATE INDEX ix_memberships_user_id_status ON memberships (user_id, status);

CREATE INDEX ix_payments_leaderboard_id ON payments (leaderboard_id);

CREATE UNIQUE INDEX ix_payments_provider_provider_ref ON payments (provider, provider_ref);

CREATE INDEX ix_payments_user_id ON payments (user_id);

CREATE UNIQUE INDEX ix_scores_event_id_user_id ON scores (event_id, user_id);

CREATE INDEX ix_scores_user_id ON scores (user_id);

CREATE UNIQUE INDEX ix_users_email ON users (email);

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
VALUES ('20261127135425_InitialCreate', '10.0.15');

COMMIT;

