using System;
using System.Text;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace ReturnHome.Server.EntityObject
{
    public partial class Entity
    {
        private static float _speedAdjust = 6.4f;

        public void ObjectUpdateObjectID() => MemoryMarshal.Write(ObjectUpdate.Span[0..], ref _objectID);

        public void ObjectUpdatePosition()
        {
            // Set 0x82 at span[4]
            ObjectUpdate.Span[4] = 0x82;

            int xInt = (int)(Position.X * 128.0f);
            int yInt = (int)(Position.Y * 128.0f);
            int zInt = (int)(Position.Z * 128.0f);

            // Convert each integer to a 3-byte big-endian representation and store in ObjectUpdate.Span
            WriteInt24BigEndian(ObjectUpdate.Span[5..], xInt);
            WriteInt24BigEndian(ObjectUpdate.Span[8..], yInt);
            WriteInt24BigEndian(ObjectUpdate.Span[11..], zInt);
        }
        private void WriteInt24BigEndian(Span<byte> span, int value)
        {
            span[0] = (byte)((value >> 16) & 0xFF);
            span[1] = (byte)((value >> 8) & 0xFF);
            span[2] = (byte)(value & 0xFF);
        }

        //public void ObjectUpdateEntity(byte temp = 0x82) => ObjectUpdate.Span[4] = temp;

        //public void ObjectUpdatePosition()
        //{
        //    Span<byte> temp = ObjectUpdate.Span;

        //    BinaryPrimitives.WriteInt32BigEndian(temp[10..], (int)(Position.Z * 128.0f));
        //    BinaryPrimitives.WriteInt32BigEndian(temp[7..], (int)(Position.Y * 128.0f));
        //    BinaryPrimitives.WriteInt32BigEndian(temp[4..], (int)(Position.X * 128.0f));

        //    //Critical to make sure that writing the int's don't override the facing value
        //    ObjectUpdateEntity();
        //}               

        public void ObjectUpdateFacing() => ObjectUpdate.Span[14] = Facing;

        public void ObjectUpdateFirstPerson() => ObjectUpdate.Span[15] = FirstPerson;

        public void ObjectUpdateWorld() => ObjectUpdate.Span[16] = (byte)World;

        public void ObjectUpdateKillTime() => MemoryMarshal.Write(ObjectUpdate.Span[17..], ref _killTime);

        public void ObjectUpdateHPFlag() => ObjectUpdate.Span[25] = HPFlag ? (byte)1 : (byte)3;

        public void ObjectUpdateHPBar()
        {
            ObjectUpdate.Span[26] = (byte)(255f * CurrentHP / HPMax);
            HPFlag = true;
            if (ObjectUpdate.Span[26] == 0)
                HPFlag = false;
        }

        public void ObjectUpdateModelID() => MemoryMarshal.Write(ObjectUpdate.Span[27..], ref _modelID);

        public void ObjectUpdateModelIDUpgraded() => MemoryMarshal.Write(ObjectUpdate.Span[31..], ref _modelID);

        public void ObjectUpdateModelSize() => MemoryMarshal.Write(ObjectUpdate.Span[35..], ref _modelSize);

        public void ObjectUpdateVelocityX()
        {
            ushort svx = VelocityX;
            MemoryMarshal.Write(ObjectUpdate.Span[39..], ref svx);
        }

        public void ObjectUpdateVelocityY()
        {
            //sbyte svy = (sbyte)Math.Round(VelocityY * _speedAdjust);
            //if (svy > 127) { Console.WriteLine("WARNING: svx=" + svy); svy = 127; }
            //if (svy < -128) { Console.WriteLine("WARNING: svx=" + svy); svy = -128; }
            ushort svy = VelocityY;
            MemoryMarshal.Write(ObjectUpdate.Span[41..], ref svy);
        }

        public void ObjectUpdateVelocityZ()
        {
            ushort svz = VelocityZ;
            MemoryMarshal.Write(ObjectUpdate.Span[41..], ref svz);
        }

        public void ObjectUpdateEastWest() => ObjectUpdate.Span[44] = EastToWest;

        public void ObjectUpdateLateralMovement() => ObjectUpdate.Span[45] = LateralMovement;

        public void ObjectUpdateNorthSouth() => ObjectUpdate.Span[46] = NorthToSouth;

        public void ObjectUpdateTurning() => ObjectUpdate.Span[47] = Turning;

        public void ObjectUpdateSpinDown() => ObjectUpdate.Span[48] = SpinDown;

        public void ObjectUpdateCoordY() => MemoryMarshal.Write(ObjectUpdate.Span[49..], ref y);

        public void ObjectUpdateFacingF() => MemoryMarshal.Write(ObjectUpdate.Span[53..], ref _facingF);

        public void ObjectUpdateAnimation() => ObjectUpdate.Span[57] = Animation;

        public void ObjectUpdateTarget() => MemoryMarshal.Write(ObjectUpdate.Span[58..], ref _target);

        public void ObjectUpdateUnknown(ushort temp = 0x0501)
        {
            MemoryMarshal.Write(ObjectUpdate.Span[62..], ref temp);
        }

        //public void ObjectUpdateUnknown2(byte temp = 0x01)
        //{
        //    MemoryMarshal.Write(ObjectUpdate.Span[189..], ref temp);
        //}

        public void ObjectUpdatePrimary() => MemoryMarshal.Write(ObjectUpdate.Span[66..], ref _primary);

        public void ObjectUpdateSecondary() => MemoryMarshal.Write(ObjectUpdate.Span[70..], ref _secondary);

        public void ObjectUpdateShield() => MemoryMarshal.Write(ObjectUpdate.Span[74..], ref _shield);

        public void ObjectUpdatePrimaryUpgraded() => MemoryMarshal.Write(ObjectUpdate.Span[78..], ref _primary);

        public void ObjectUpdateSecondaryUpgraded() => MemoryMarshal.Write(ObjectUpdate.Span[82..], ref _secondary);

        public void ObjectUpdateShieldUpgraded() => MemoryMarshal.Write(ObjectUpdate.Span[86..], ref _shield);

        public void ObjectUpdateChest() => ObjectUpdate.Span[92] = Chest;

        public void ObjectUpdateBracer() => ObjectUpdate.Span[93] = Bracer;

        public void ObjectUpdateGloves() => ObjectUpdate.Span[94] = Gloves;

        public void ObjectUpdateLegs() => ObjectUpdate.Span[95] = Legs;

        public void ObjectUpdateBoots() => ObjectUpdate.Span[96] = Boots;

        public void ObjectUpdateHelm() => ObjectUpdate.Span[97] = Helm;

        public void ObjectUpdateVanillaColors()
        {
            ushort temp = 0xFFFF;
            for (int i = 0; i < 6; i++)
                MemoryMarshal.Write(ObjectUpdate.Span[(98 + (i * 2))..], ref temp);
        }

        public void ObjectUpdateChestColor() => BinaryPrimitives.WriteUInt32BigEndian(ObjectUpdate.Slice(110, 4).Span, ChestColor);

        public void ObjectUpdateBracerColor() => BinaryPrimitives.WriteUInt32BigEndian(ObjectUpdate.Slice(115, 4).Span, BracerColor);

        public void ObjectUpdateGloveColor() => BinaryPrimitives.WriteUInt32BigEndian(ObjectUpdate.Slice(120, 4).Span, GloveColor);

        public void ObjectUpdateLegColor() => BinaryPrimitives.WriteUInt32BigEndian(ObjectUpdate.Slice(125, 4).Span, LegColor);

        public void ObjectUpdateBootsColor() => BinaryPrimitives.WriteUInt32BigEndian(ObjectUpdate.Slice(130, 4).Span, BootsColor);

        public void ObjectUpdateHelmColor() => BinaryPrimitives.WriteUInt32BigEndian(ObjectUpdate.Slice(135, 4).Span, HelmColor);

        public void ObjectUpdateRobeColor() => BinaryPrimitives.WriteUInt32BigEndian(ObjectUpdate.Slice(140, 4).Span, RobeColor);

        public void ObjectUpdateHairColor() => ObjectUpdate.Span[144] = (byte)HairColor;

        public void ObjectUpdateHairLength() => ObjectUpdate.Span[145] = (byte)HairLength;

        public void ObjectUpdateHairStyle() => ObjectUpdate.Span[146] = (byte)HairStyle;

        public void ObjectUpdateFaceOption() => ObjectUpdate.Span[147] = (byte)FaceOption;

        public void ObjectUpdateRobe() => ObjectUpdate.Span[148] = (byte)Robe;

        public void ObjectUpdateName()
        {
            Span<byte> span3 = ObjectUpdate.Span;
            span3[154..177].Fill(0);
            ReadOnlySpan<char> span2 = _charName.AsSpan();
            for (int i = 0; i < span2.Length; ++i)
                span3[154 + i] = (byte)span2[i];
        }

        public void ObjectUpdateLevel() => ObjectUpdate.Span[178] = (byte)Level;

        public void ObjectUpdateMovement(byte temp = 1) => ObjectUpdate.Span[179] = temp;

        public void ObjectUpdateNameColor(byte color = 255)
        {
            if (isPlayer)
                ObjectUpdate.Span[180] = color == 255 ? (byte)2 : color;
            else
                ObjectUpdate.Span[180] = color == 255 ? (byte)0 : color;
        }

        public void ObjectUpdateNamePlate(byte color = 255) => ObjectUpdate.Span[181] = color == 255 ? (byte)0 : color;

        public void ObjectUpdateRace() => ObjectUpdate.Span[182] = (byte)EntityRace;

        public void ObjectUpdateClass() => ObjectUpdate.Span[183] = (byte)EntityClass;

        public void ObjectUpdateNPCType() => MemoryMarshal.Write(ObjectUpdate.Span[184..], ref _npcType);

        public void ObjectUpdatePattern(uint stuff = 0xFFFFFFFF) => BitConverter.GetBytes(stuff).CopyTo(ObjectUpdate.Slice(186, 4));

        public void ObjectUpdateStatus(byte status = 0) => ObjectUpdate.Span[192] = status;

        public void ObjectUpdateOnline(byte status = 1) => ObjectUpdate.Span[194] = status;

        public void ObjectUpdateEnd() => Encoding.UTF8.GetBytes("trsq").CopyTo(ObjectUpdate.Slice(196, 4));
    }
}
