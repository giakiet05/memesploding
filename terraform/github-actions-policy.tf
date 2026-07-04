data "aws_iam_policy_document" "github_actions_permissions" {
  statement {
    sid    = "LoginToECR"
    effect = "Allow"

    actions = [
      "ecr:GetAuthorizationToken"
    ]

    resources = ["*"]
  }

  statement {
    sid    = "PushImagesToECR"
    effect = "Allow"

    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:CompleteLayerUpload",
      "ecr:InitiateLayerUpload",
      "ecr:PutImage",
      "ecr:UploadLayerPart"
    ]

    resources = [
      aws_ecr_repository.api_server_repo.arn,
      aws_ecr_repository.game_server_repo.arn
    ]
  }

  statement {
    sid    = "ManageTaskDefinitions"
    effect = "Allow"

    actions = [
      "ecs:DescribeTaskDefinition",
      "ecs:RegisterTaskDefinition"
    ]

    resources = ["*"]
  }

  statement {
    sid    = "TagTaskDefinitions"
    effect = "Allow"

    actions = [
      "ecs:TagResource"
    ]

    resources = [
      "${aws_ecs_task_definition.api.arn_without_revision}:*",
      "${aws_ecs_task_definition.game.arn_without_revision}:*"
    ]
  }

  statement {
    sid    = "DeployECSServices"
    effect = "Allow"

    actions = [
      "ecs:DescribeServices",
      "ecs:UpdateService"
    ]

    resources = [
      aws_ecs_service.api.id,
      aws_ecs_service.game.id
    ]
  }

  statement {
    sid    = "PassECSTaskExecutionRole"
    effect = "Allow"

    actions = [
      "iam:PassRole"
    ]

    resources = [
      aws_iam_role.ecs_task_execution_role.arn
    ]

    condition {
      test     = "StringEquals"
      variable = "iam:PassedToService"

      values = [
        "ecs-tasks.amazonaws.com"
      ]
    }
  }
}

resource "aws_iam_role_policy" "github_actions" {
  name   = "memesploding-github-actions-policy"
  role   = aws_iam_role.github_actions.id
  policy = data.aws_iam_policy_document.github_actions_permissions.json
}
