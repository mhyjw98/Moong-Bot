//using Discord;
//using Discord.Commands;
//using Discord.Interactions;
//using MoongBot.Core.Manager;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace MoongBot.Core.Commands
//{
//    public class TestCommands : ModuleBase<SocketCommandContext>
//    {
//        private readonly TestManager _testManager;
//        public TestCommands()
//        {
//            var lottoManager = new LottoManager();
//            var dbManager = new DatabaseManager();

//            _testManager = new TestManager(lottoManager, dbManager);
//        }

//        [Command("테스트슬롯")]
//        [Remarks("테스트용 슬롯 머신 명령어.")]
//        [Hidden]
//        public async Task TestSlotMachineCommand([Remainder] int input)
//        {
//            if (Context.User.Id != ConfigManager.Config.OwnerId)
//            {
//                return;
//            }

//            try
//            {
//                var channel = Context.Channel as ITextChannel;
//                int numberOfTests = 1000000;
//                await _testManager.TestRunSlotMachineMultipleTimes(channel, Context.Guild, numberOfTests, input);
//            }
//            catch(Exception ex)
//            {
//                Console.WriteLine($"에러 발생 : {ex.Message}");
//            }                       
//        }
//    }
//}
