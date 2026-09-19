# 设计调研：当一次计算变成一段用户旅程

调研日期：2026-09-20。以下是历史设计机制与可查阅的用户反馈，不是对所有 Windows 版本、所有用户的概括。部分机制正在随版本调整。夸张的计算器功能是本项目的创作，不是微软产品的真实功能。

| 已有机制 / 用户反馈 | 原始资料 | SuperCalc 的讽刺转译 |
| --- | --- | --- |
| Windows 11 的新菜单将旧扩展放在“显示更多选项”中；用户抱怨常用命令多一步 | [微软设计说明，2021-07-19](https://blogs.windows.com/blog/2021/07/19/extending-the-context-menu-and-share-dialog-in-windows-11/)；[用户要求撤掉二级菜单](https://learn.microsoft.com/en-us/answers/questions/4059405/windows-11s-right-click-context-menu-has-got-to-go) | 复制结果先打开现代菜单，再打开传统菜单 |
| 个性化建议可包含广告，系统的任务入口同时承载推广 | [微软设备使用设置说明](https://support.microsoft.com/en-us/windows/experience/personalization/personalize-your-windows-experience-with-device-usage-settings)；[推荐与优惠隐私设置](https://support.microsoft.com/en-gb/windows/privacy/privacy-settings-for-recommendations-offers-in-windows-11) | 推荐数字、365 位数套餐、云端小数点、赞助卡片 |
| 已使用多年的设备仍出现“完成设置”提示 | [用户反馈](https://www.reddit.com/r/microsoftsucks/comments/1vat1cv/this_is_a_5_year_old_install_that_still_wants_me/)；[另一用户反馈](https://www.reddit.com/r/WindowsHelp/comments/1dkzdsd) | 首次启动多页向导、账户利益卡、“稍后提醒我” |
| 记事本等简单工具加入 AI；2024 年预览版 Rewrite 要求登录并使用积分 | [微软 Notepad AI 发布说明，2024-11-06](https://blogs.windows.com/windows-insider/2024/11/06/new-ai-experiences-for-paint-and-notepad-begin-rolling-out-to-windows-insiders/) | CalcPilot 重新想象 2+2、思考动画、虚构积分和结果语气 |
| 系统搜索与 Bing 网页结果混合，有用户要求移除 | [用户反馈](https://learn.microsoft.com/en-us/answers/questions/2343640/how-to-permanently-remove-bing-from-windows-11) | 本地搜索先展示网页式推荐，再显示真正匹配的历史 |
| 新旧界面并存，微软持续逐批更新旧对话框 | [微软 2025-10-16 更新说明](https://blogs.windows.com/windowsexperience/2025/10/16/new-experiences-currently-rolling-out-for-windows-11/) | Fluent 设置中嵌入复古数值控制面板 |

## 设计边界

- 讽刺对象是打断工作、隐藏操作与过度推广，不把扁平设计本身等同于不可用。
- 所有“账号 / 云 / AI / 订阅 / 更新”只在应用内模拟。没有真的账号输入、付款、网络上传或系统更改。
- 保持正确的计算、键盘操作、可访问名称、明确的错误反馈、可撤销的设置。
- 专注模式直接关闭干扰。演示模式把仪式感拉满，但退出永远保留直接关闭选项。
