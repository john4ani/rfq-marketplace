variable "name" {
  type = string
}

variable "instance_name" {
  type = string
}

variable "alert_email" {
  type = string
}

variable "tags" {
  type    = map(string)
  default = {}
}
