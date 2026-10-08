package auth

import (
	"context"
	"database/sql"
	"errors"

	mssql "github.com/microsoft/go-mssqldb"
)

var (
	ErrEmailAlreadyExists = errors.New("email already exists")
	ErrRoleNotFound       = errors.New("default role not found")
)

type Repository interface {
	CreateSolicitante(
		ctx context.Context,
		name string,
		email string,
		passwordHash string,
	) (User, error)
}

type SQLRepository struct {
	db *sql.DB
}

func NewSQLRepository(db *sql.DB) *SQLRepository {
	return &SQLRepository{db: db}
}

func (r *SQLRepository) CreateSolicitante(
	ctx context.Context,
	name string,
	email string,
	passwordHash string,
) (User, error) {

	tx, err := r.db.BeginTx(ctx, nil)
	if err != nil {
		return User{}, err
	}

	defer tx.Rollback()

	var exists bool

	err = tx.QueryRowContext(
		ctx,
		`SELECT CASE WHEN EXISTS (
			SELECT 1
			FROM dbo.Usuario
			WHERE Correo = @p1
		) THEN 1 ELSE 0 END`,
		email,
	).Scan(&exists)

	if err != nil {
		return User{}, err
	}

	if exists {
		return User{}, ErrEmailAlreadyExists
	}

	var userID int64

	err = tx.QueryRowContext(
		ctx,
		`
		INSERT INTO dbo.Usuario
			(Nombre, Correo, PasswordHash, Activo)
		OUTPUT INSERTED.IdUsuario
		VALUES
			(@p1, @p2, @p3, 1)
		`,
		name,
		email,
		passwordHash,
	).Scan(&userID)

	if err != nil {
		var sqlErr mssql.Error

		if errors.As(err, &sqlErr) &&
			(sqlErr.Number == 2601 || sqlErr.Number == 2627) {
			return User{}, ErrEmailAlreadyExists
		}

		return User{}, err
	}

	var roleID int64

	err = tx.QueryRowContext(
		ctx,
		`
		SELECT IdRol
		FROM dbo.Rol
		WHERE Nombre = N'Solicitante'
		  AND Activo = 1
		`,
	).Scan(&roleID)

	if errors.Is(err, sql.ErrNoRows) {
		return User{}, ErrRoleNotFound
	}

	if err != nil {
		return User{}, err
	}

	_, err = tx.ExecContext(
		ctx,
		`
		INSERT INTO dbo.UsuarioRol
			(IdUsuario, IdRol)
		VALUES
			(@p1, @p2)
		`,
		userID,
		roleID,
	)

	if err != nil {
		return User{}, err
	}

	if err := tx.Commit(); err != nil {
		return User{}, err
	}

	return User{
		ID:    userID,
		Name:  name,
		Email: email,
		Role:  "Solicitante",
	}, nil
}
