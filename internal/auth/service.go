package auth

import (
	"context"
	"errors"
	"net/mail"
	"strings"
	"unicode/utf8"

	"golang.org/x/crypto/bcrypt"
)

var ErrInvalidInput = errors.New("invalid input")

type Service struct {
	repository Repository
}

func NewService(repository Repository) *Service {
	return &Service{
		repository: repository,
	}
}

func (s *Service) Register(
	ctx context.Context,
	request RegisterRequest,
) (User, error) {

	name := strings.TrimSpace(request.Name)
	email := strings.ToLower(strings.TrimSpace(request.Email))

	if utf8.RuneCountInString(name) < 3 ||
		utf8.RuneCountInString(name) > 150 {
		return User{}, ErrInvalidInput
	}

	address, err := mail.ParseAddress(email)
	if err != nil || address.Address != email {
		return User{}, ErrInvalidInput
	}

	if len(request.Password) < 8 || len(request.Password) > 72 {
		return User{}, ErrInvalidInput
	}

	hash, err := bcrypt.GenerateFromPassword(
		[]byte(request.Password),
		bcrypt.DefaultCost,
	)

	if err != nil {
		return User{}, err
	}

	return s.repository.CreateSolicitante(
		ctx,
		name,
		email,
		string(hash),
	)
}
