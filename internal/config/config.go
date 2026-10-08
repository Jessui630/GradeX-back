package config

import (
	"fmt"
	"os"
)

type Config struct {
	Port               string
	DBConnectionString string
	FrontendOrigin     string
}

func Load() (Config, error) {
	cfg := Config{
		Port:               getEnv("PORT", "8080"),
		DBConnectionString: os.Getenv("DB_CONNECTION_STRING"),
		FrontendOrigin:     getEnv("FRONTEND_ORIGIN", "http://localhost:5173"),
	}

	if cfg.DBConnectionString == "" {
		return Config{}, fmt.Errorf("DB_CONNECTION_STRING is required")
	}

	return cfg, nil
}

func getEnv(key, fallback string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}

	return fallback
}
