variable "db_password" {
  description = "Password for Postgres RDS database"
  type = string
  sensitive = true # This hide the password from showing up in the terminal output
}

variable "jwt_key" {
  description = "Secret key for JWT authentication"
  type = string
  sensitive = true
}

variable "google_client_id" {
  description = "Google OAuth Client ID"
  type = string
}

variable "google_client_secret" {
  description = "Google OAuth Client Secret"
  type = string
  sensitive = true
}

variable "game_ticket_key" {
  description = "Secret key for Game Ticket generation"
  type = string
  sensitive = true
}

variable "custom_domain" {
  description = "Custom domain name for the application (e.g. memesploding.giakiet.io.vn)"
  type = string
}
