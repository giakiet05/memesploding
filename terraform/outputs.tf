output "alb_dns_name" {
  description = "The DNS name of the Application Load Balancer"
  value       = aws_lb.main.dns_name
}

output "api_url" {
  description = "The URL to access the API Server"
  value       = "http://${aws_lb.main.dns_name}/health"
}

output "game_url" {
  description = "The URL to access the Game Server"
  value       = "http://${aws_lb.main.dns_name}/ws/health"
}
