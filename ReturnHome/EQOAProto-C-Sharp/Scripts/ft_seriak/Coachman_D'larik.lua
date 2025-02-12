﻿-- coachman d'larik'

local coaches = require('Scripts/ports')

local playerCoaches = {
   highpass_coach = "Get me a horse to Highpass.",
   mt_hatespike_coach = "Get me a horse to the Last Inn.",
   neriak_coach = "Get me a horse to the dark city of Neriak."
}

local dialogueOptions = {}
local ch = tostring(choice)
function event_say()
   if(GetPlayerFlags(mySession, "fort_seriak_coach") == "true") then
      if (ch:find("Highpass")) then
         TeleportPlayer(mySession,GetWorld(coaches.highpass.world),coaches.highpass.x,coaches.highpass.y,coaches.highpass.z,coaches.highpass.facing)
      elseif (ch:find("Inn")) then
         TeleportPlayer(mySession,GetWorld(coaches.mt_hatespike.world),coaches.mt_hatespike.x,coaches.mt_hatespike.y,coaches.mt_hatespike.z,coaches.mt_hatespike.facing)
      elseif (ch:find("Neriak")) then
         TeleportPlayer(mySession,GetWorld(coaches.neriak.world),coaches.neriak.x,coaches.neriak.y,coaches.neriak.z,coaches.neriak.facing)
      else
         npcDialogue = "Where would you like to go?"
         for coach, diag in pairs(playerCoaches) do
            if ((GetPlayerFlags(mySession, "admin")  or GetPlayerFlags(mySession, coach)) == "true") then
               table.insert(dialogueOptions, diag)
            end
         end
         SendDialogue(mySession, npcDialogue, dialogueOptions)
      end
   else
      if (ch:find("Yes")) then
         npcDialogue = "Excellent, you can now use this coach any time."
         SetPlayerFlags(mySession, "fort_seriak_coach","true")
         SendDialogue(mySession, npcDialogue, dialogueOptions)
      elseif (ch:find("No")) then
         npcDialogue = "If you aren't interested then why are you wasting my time."
         SendDialogue(mySession, npcDialogue, dialogueOptions)
      else
         npcDialogue = "Would you like to sign the coachman's ledger?"
         dialogueOptions = {"Yes", "No"}
         SendDialogue(mySession, npcDialogue, dialogueOptions)
      end
   end
end
