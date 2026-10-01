package main

import (
	"encoding/json"
	"log"
	"net/http"
	"os"
)

type HealthResponse struct {
	Status      string `json:"status"`
	Application string `json:"application"`
	Version     string `json:"version"`
	Sprint      string `json:"sprint"`
}

func main() {

	http.HandleFunc("/api/health", func(w http.ResponseWriter, r *http.Request) {

		w.Header().Set("Content-Type", "application/json")

		response := HealthResponse{
			Status:      "ok",
			Application: "GradeX API",
			Version:     "0.1.0",
			Sprint:      "Sprint 1",
		}

		json.NewEncoder(w).Encode(response)
	})

	port := os.Getenv("PORT")

	if port == "" {
		port = "8080"
	}

	log.Printf("GradeX API ejecutándose en puerto %s", port)

	err := http.ListenAndServe(":"+port, nil)

	if err != nil {
		log.Fatal(err)
	}
}