# CreateSpawn 复制建筑

- 作者: 少司命 羽学
- 出处: [CreateSpawn](https://github.com/1242509682/CreateSpawn)
- 这是一个Tshock服务器插件，主要用于：创建地图时使你的新地图支持复制建筑，使用指令在头顶生成建筑，不再固定为出生点


## 指令

| 语法      |    权限     |        说明        |
| --------- | :---------: | :----------------: |
|  /cb sv   | create.copy       |   复制建筑 |
|  /cb pt   | create.copy       |   粘贴建筑 |
|  /cb fix  | create.copy       |   修复图格 |
|  /cb bk   | create.copy       |   撤销操作 |
|  /cb t    | create.admin      |   操作图格 |
|  /cb r    | create.admin      |   修改区域 |
|  /cb c    | create.admin      |   修改配置 |
|  /reload  | tshock.cfg.reload |   重载配置 |

## 更新日志
```
v2.0.0
重构代码，适配泰拉瑞亚1.4.5.6 与 TShock 6.1.0
移除了访客记录功能与进度限制功能
加入了管理专用表，区域表等配置项
加入了修复使用快照局部图格功能、批量修改图格、区域修改等功能
以上功能均来自145修复小公举，移除了set指令统一用精密线控仪触发
```

## 反馈
- 优先发issued -> 共同维护的插件库：https://github.com/UnrealMultiple/TShockPlugin
- 次优先：TShock官方群：816771079
- 大概率看不到但是也可以：国内社区trhub.cn ，bbstr.net , tr.monika.love
