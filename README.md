# CreateSpawn 复制建筑

- 作者: 少司命 羽学
- 出处: 无
- 这是一个Tshock服务器插件，主要用于：创建地图时使你的新地图支持复制建筑，使用指令在头顶生成建筑，不再固定为出生点


## 指令

| 语法      |    权限     |        说明        |
| --------- | :---------: | :----------------: |
| /cb on | create.copy |   开启插件   |
| /cb off | create.copy |   关闭插件   |
| /cb s 1 | create.copy |   敲击或放置一个方块到左上角   |
| /cb s 2 | create.copy |   敲击或放置一个方块到右下角   |
| /cb add 名字  | create.copy | 保存建筑 |
| /cb sp 名字  | create.copy |      生成建筑      |
| /cb bk   | create.copy |      还原建筑覆盖图格      |
| /cb list   | create.copy |      列出已有建筑     |

## 配置
> 配置文件位置：tshock/CreateSpawn.json
```json5
{
  "出生点生成": false,
  "修复晶塔": false,
  "centreX": 145, //不要动
  "CountY": 178, //不要动
  "微调X": -21,
  "微调Y": 50
}
```

## 更新日志
```
v1.0.0.7
Nuget包采用为Tshock 5.2.4
重构代码逻辑，实现跨服复制建筑
启动服务器时：未检测到CreateSpawn文件夹时则自动创建，并生成一个内嵌的“出生点_cp.map”文件
在生成新地图时，自动会按名字为“出生点”建筑重建
重构/cb spawn指令：
当控制台使用`/cb sp 出生点`会以出生点为坐标进行生成(可通过配置文件调整),控制台使用/cb bk可撤销此建筑
玩家使用`/cb sp 出生点`则以玩家头顶生成该建筑
加入了/cb list指令：可列出所有可用的建筑名单
加入了/cb zip指令：清空建筑文件并备份为zip
修复了粘贴建筑时，无法复制箱子内物品与标牌内容信息的BUG
加入了“修复晶塔”配置项，不推荐打开，使用/cb bk撤销时会留个晶塔传送图标在地图上
使用了GZipStream极大降低了对建筑文件的空间占用

v1.0.0.6
Nuget包采用为Tshock 5.2.2
羽学内测版（此版本不依赖LazyAPI）：
插件更名为《复制建筑》
根据使用指令在头顶生成建筑，不再固定为出生点
将/create指令更名为/cb spawn
加入了/cb back指令，可还原建筑覆盖区域
注意：本插件需要在新地图时才会生效，
请删除tshock文件夹下对应的CreateSpawn.map文件再放入新地图

v1.0.0.3
使用lazyapi
v1.0.0.2
i18n预定
v1.0.0.1
补全卸载函数
```

## 反馈
- 优先发issued -> 共同维护的插件库：https://github.com/UnrealMultiple/TShockPlugin
- 次优先：TShock官方群：816771079
- 大概率看不到但是也可以：国内社区trhub.cn ，bbstr.net , tr.monika.love
