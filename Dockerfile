FROM golang:1.27-alpine AS builder

WORKDIR /app

COPY go.mod ./

RUN go mod download

COPY . .

RUN CGO_ENABLED=0 GOOS=linux go build -o gradex-api .

FROM alpine:latest

WORKDIR /app

COPY --from=builder /app/gradex-api .

EXPOSE 8080

CMD ["./gradex-api"]